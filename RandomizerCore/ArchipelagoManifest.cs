using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Z2Randomizer.RandomizerCore.Overworld;
using Z2Randomizer.RandomizerCore.Sidescroll;
using Z2Randomizer.RandomizerCore.Sidescroll.Palace;
using Z2Randomizer.RandomizerCore.Sidescroll.Town;

namespace Z2Randomizer.RandomizerCore;

public enum ApFlagKind
{
    /// <summary>A bit in the game's per-room item presence table. It starts at 1 and is cleared when the item is picked up.</summary>
    ItemPresence,
    /// <summary>A bit in the wizard flags. It is set when the item is received.</summary>
    Wizard,
    /// <summary>A bit in the misc flags (trainers, Bagu, mirror table, fountain). It is set when the item is received.</summary>
    Misc,
    /// <summary>A flag set when an event happens, such as placing a crystal in a palace statue. The location holds a fixed item.</summary>
    Event,
}

/// <param name="NodeId">The id of the node in the requirement graph.</param>
/// <param name="Address">CPU address of the RAM byte holding the flag.</param>
/// <param name="Mask">The bit(s) of that byte that change when the location is checked.</param>
/// <param name="CollectedBitValue">Value (0 or 1) the bit has once the location has been checked.</param>
/// <param name="FixedItem">The Archipelago item the location always holds, such as "Crystal 1". Null when the multiworld decides.</param>
public record ApLocation(
    string NodeId,
    string Name,
    ApFlagKind Kind,
    int Address,
    int Mask,
    int CollectedBitValue,
    string? FixedItem = null);

public record ApManifest(
    string? Seed,
    string? Flags,
    string SlotName,
    string Description,
    List<ApLocation> Locations,
    List<string> Warnings);

[JsonSourceGenerationOptions(
    UseStringEnumConverter = true,
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(ApManifest))]
public partial class ApManifestSerializationContext : JsonSerializerContext { }

/// <summary>
/// Lists the RAM flag the game sets when each AP item location is checked, so an emulator can watch them.
/// The addresses follow the item pickup code: the item presence tables in bank 7 (see Hyrule's
/// bank7_Pointer_table_for_Item_Presence tables) and the custom wizard and misc flags in FullItemShuffle.s.
/// </summary>
public static class ApManifestBuilder
{
    private const string DESCRIPTION =
        "Each entry is a location that holds an AP item. Read the byte at address and test it against mask: the location has "
        + "been checked when (byte & mask) is 0 (collectedBitValue 0) or non-zero (collectedBitValue 1). "
        + "nodeId matches the node ids in the requirements file.";

    public const int WIZARD_FLAGS = 0x077a;
    public const int MISC_FLAGS = 0x0795;
    /// <summary>The crystal-placed flags of palaces 1 to 6 are the consecutive bytes after this (Crystal1Placed in ram.inc).</summary>
    public const int CRYSTAL_PLACED_FLAGS = 0x078c;

    private const int CAVES_WEST = 0x0600;
    private const int CAVES_DM_AND_MAZE = 0x0620;
    private const int CAVES_EAST = 0x0640;
    private const int TOWNS = 0x0660;
    private const int PALACE_125 = 0x0680;
    private const int PALACE_346 = 0x06a0;

    // The game's JankPowerOfTwo table, indexed by town number
    private static readonly int[] TOWN_BIT = [0x08, 0x04, 0x02, 0x01, 0x80, 0x40, 0x20, 0x10];

    /// <summary>The RAM byte holding the item presence bits for a map, given the base of the table it is in.</summary>
    public static int ItemPresenceAddress(int tableBase, int map) => tableBase + (map >> 1);

    /// <summary>
    /// The item presence bit for an item on the given screen (page) of a map. Even maps use the high nibble
    /// of the byte and odd maps the low nibble, and the screen number selects the bit within the nibble.
    /// </summary>
    public static int ItemPresenceMask(int map, int page)
    {
        if (page is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(page), $"An item on screen {page} of map {map} has no item presence bit");
        }
        return (map & 1) == 0 ? 0x80 >> page : 0x08 >> page;
    }

    private static int CavesBase(Continent continent) => continent switch
    {
        Continent.WEST => CAVES_WEST,
        Continent.DM => CAVES_DM_AND_MAZE,
        Continent.EAST => CAVES_EAST,
        Continent.MAZE => CAVES_DM_AND_MAZE,
        _ => throw new ArgumentOutOfRangeException(nameof(continent))
    };

    /// <summary>The screen of the first pick-up-able item in a sideview, which is what the game uses as its bit number.</summary>
    private static int ItemPage<T>(SideviewEditable<T> sideview, string description) where T : struct, Enum
    {
        var item = sideview.Commands.FirstOrDefault(c => c.IsItem() && !c.Extra.IsMinorItem())
            ?? throw new ImpossibleException($"No item found in the sideview of AP item location {description}");
        return item.Page;
    }

    public static ApManifest Build(IReadOnlyList<World> worlds, IEnumerable<Location> itemLocations, ROM rom, RandomizerProperties props)
    {
        List<ApLocation> locations = [];
        foreach (var location in itemLocations)
        {
            if (location.Town != null)
            {
                AddTown(locations, location, location.Town);
            }
            else if (location.Palace != null)
            {
                AddPalace(locations, location, location.Palace);
            }
            else if (location.GetAllCollectables().Any(c => c == Collectable.AP_ITEM))
            {
                AddOverworld(locations, location, worlds.First(w => w.AllLocations.Contains(location)), rom);
            }
        }

        locations.AddRange(PalaceCrystalEvents(worlds.SelectMany(w => w.AllLocations)));

        List<string> warnings = [];
        foreach (var group in locations.GroupBy(l => (l.Address, l.Mask)).Where(g => g.Count() > 1))
        {
            warnings.Add($"{string.Join(", ", group.Select(l => l.NodeId))} share the flag at "
                + $"0x{group.Key.Address:x4} mask 0x{group.Key.Mask:x2}, so they are checked together.");
        }

        return new ApManifest(props.Seed, props.Flags, props.ArchipelagoSlotName, DESCRIPTION, locations, warnings);
    }

    /// <summary>
    /// Placing the crystal in the statue of a palace after its boss is a location that sends out "Crystal N", even when
    /// the palace holds no AP item. The Great Palace has no crystal.
    /// </summary>
    public static IEnumerable<ApLocation> PalaceCrystalEvents(IEnumerable<Location> locations)
    {
        foreach (var location in locations.Where(l => l.Palace is { Number: >= 1 and <= 6 }))
        {
            var palace = location.Palace!;
            yield return new ApLocation(GraphNodeIds.PalaceCrystal(location), $"{PalaceName(palace)} crystal",
                ApFlagKind.Event, CRYSTAL_PLACED_FLAGS + palace.Number, 0xff, 1, $"Crystal {palace.Number}");
        }
    }

    // Where the item byte of each overworld location with a shuffled item is in the ROM. Entering a cave goes through a
    // connection table, so the map the item sits in can differ from the location's own map number.
    // The map is found from the item's address instead.
    private static readonly Dictionary<LocationID, int> OVERWORLD_ITEM_ADDRESSES = new()
    {
        { LocationID.WEST_GRASS, RomMap.WEST_GRASS_TILE_COLLECTABLE },
        { LocationID.WEST_CAVE_TROPHY, RomMap.WEST_TROPHY_CAVE_COLLECTABLE },
        { LocationID.WEST_CAVE_HEART_CONTAINER, RomMap.WEST_HEART_CONTAINER_CAVE_COLLECTABLE },
        { LocationID.WEST_CAVE_MAGIC_CONTAINER, RomMap.WEST_MAGIC_CONTAINER_CAVE_COLLECTABLE },
        { LocationID.WEST_CAVE_MEDICINE, RomMap.WEST_MEDICINE_CAVE_COLLECTABLE },
        { LocationID.DM_HAMMER_CAVE, RomMap.DM_HAMMER_CAVE_COLLECTABLE },
        { LocationID.DM_SPEC_ROCK, RomMap.DM_SPECTACLE_ROCK_COLLECTABLE },
        { LocationID.EAST_WATER, RomMap.EAST_WATER_TILE_COLLECTABLE },
        { LocationID.EAST_DESERT, RomMap.EAST_DESERT_TILE_COLLECTABLE },
        { LocationID.MI_CHILD_DROP, RomMap.MI_CHILD_DROP_COLLECTABLE },
        { LocationID.MI_MAGIC_CONTAINER_DROP, RomMap.MI_MAGIC_CONTAINER_DROP_COLLECTABLE },
        // these only hold a major item when P-bags are shuffled
        { LocationID.WEST_CAVE_PBAG, RomMap.WEST_PBAG_CAVE_COLLECTABLE },
        { LocationID.EAST_CAVE_PBAG1, RomMap.EAST_PBAG_CAVE1_COLLECTABLE },
        { LocationID.EAST_CAVE_PBAG2, RomMap.EAST_PBAG_CAVE2_COLLECTABLE },
    };

    private static void AddOverworld(List<ApLocation> result, Location location, World world, ROM rom)
    {
        if (!OVERWORLD_ITEM_ADDRESSES.TryGetValue(location.ID, out int itemAddress))
        {
            throw new ImpossibleException($"{location.ID} has an AP item but no known item address");
        }

        // Find the map whose sideview contains the item, using the continent's sideview pointer table like the game does
        for (int map = 0; map < 64; map++)
        {
            int ptr = world.sideviewPtrTable + map * 2;
            int sideviewAddr = NesPointer.ConvertNesPtrToPrgRomAddr(world.sideviewBank, rom.GetShort(ptr + 1, ptr));
            int length = rom.GetByte(sideviewAddr);
            if (itemAddress < sideviewAddr || itemAddress >= sideviewAddr + length)
            {
                continue;
            }

            var sideview = new SideviewEditable<ForestObject>(rom.GetBytes(sideviewAddr, length));
            // each command is 2 bytes, or 3 for an item, after the 4 byte header
            int offset = sideviewAddr + 4;
            foreach (var command in sideview.Commands)
            {
                if (command.IsItem() && offset + 2 == itemAddress)
                {
                    result.Add(new ApLocation(GraphNodeIds.Location(location), LocationName(location), ApFlagKind.ItemPresence,
                        ItemPresenceAddress(CavesBase(world.continentId), map), ItemPresenceMask(map, command.Page), 0));
                    return;
                }
                offset += command.Bytes.Length;
            }
        }
        throw new ImpossibleException($"Unable to find the map of the item of {location.ID} at 0x{itemAddress:x}");
    }

    private static void AddPalace(List<ApLocation> result, Location location, Palace palace)
    {
        // The Great Palace's item rooms are never shuffled
        if (palace.Number == 7)
        {
            return;
        }
        int tableBase = palace.PalaceGroup == PalaceGrouping.Palace125 ? PALACE_125 : PALACE_346;
        for (int i = 0; i < palace.AllRooms.Count; i++)
        {
            var room = palace.AllRooms[i];
            if (!palace.ItemRooms.Contains(room) || room.Collectable != Collectable.AP_ITEM)
            {
                continue;
            }
            int page = ItemPage(new SideviewEditable<PalaceObject>(room.SideView), $"{location.ID} room {room.Name}");
            result.Add(new ApLocation(GraphNodeIds.PalaceRoom(location, palace, room), $"{PalaceName(palace)} item room",
                ApFlagKind.ItemPresence, ItemPresenceAddress(tableBase, room.Map), ItemPresenceMask(room.Map, page), 0));
        }
    }

    private static void AddTown(List<ApLocation> result, Location location, Town town)
    {
        foreach (var map in town.TownMaps.Where(m => m.Collectable == Collectable.AP_ITEM))
        {
            string nodeId = GraphNodeIds.TownMap(location, map);
            string name = $"{LocationName(location)} {map.Name}".Trim();
            switch (map.CollectableTextType)
            {
                case CollectableTextType.WIZARD when town.Type == TownType.BAGU:
                    result.Add(new ApLocation(nodeId, name, ApFlagKind.Misc, MISC_FLAGS, TOWN_BIT[3], 1));
                    break;
                case CollectableTextType.WIZARD:
                    // wizards are indexed by town, Rauru first
                    result.Add(new ApLocation(nodeId, name, ApFlagKind.Wizard, WIZARD_FLAGS, TOWN_BIT[(int)town.Type! - 1], 1));
                    break;
                case CollectableTextType.DOWNSTAB_TRAINER:
                    result.Add(new ApLocation(nodeId, name, ApFlagKind.Misc, MISC_FLAGS, TOWN_BIT[0], 1));
                    break;
                case CollectableTextType.UPSTAB_TRAINER:
                    result.Add(new ApLocation(nodeId, name, ApFlagKind.Misc, MISC_FLAGS, TOWN_BIT[1], 1));
                    break;
                case CollectableTextType.TABLE: // Saria's mirror table
                    result.Add(new ApLocation(nodeId, name, ApFlagKind.Misc, MISC_FLAGS, TOWN_BIT[2], 1));
                    break;
                case CollectableTextType.FOUNTAIN: // Nabooru's fountain
                    result.Add(new ApLocation(nodeId, name, ApFlagKind.Misc, MISC_FLAGS, TOWN_BIT[4], 1));
                    break;
                case null:
                    // An item lying in a town building, such as Granny's basement or the New Kasuto spell tower
                    int page = ItemPage(map.Sideview, $"{location.ID} {map.Name}");
                    result.Add(new ApLocation(nodeId, name, ApFlagKind.ItemPresence,
                        ItemPresenceAddress(TOWNS, map.Map), ItemPresenceMask(map.Map, page), 0));
                    break;
                default:
                    throw new ImpossibleException($"Unknown collectable text type {map.CollectableTextType}");
            }
        }
    }

    /// <summary>Palaces move between locations, so they are named for the palace itself rather than the location holding it.</summary>
    private static string PalaceName(Palace palace) => $"Palace {palace.Number}";

    private static string LocationName(Location location) =>
        string.IsNullOrEmpty(location.Name) ? location.ID.ToString() : location.Name;
}
