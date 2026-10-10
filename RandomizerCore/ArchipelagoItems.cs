using System.Collections.Generic;
using System.Linq;
using Z2Randomizer.RandomizerCore.Overworld;
using Z2Randomizer.RandomizerCore.Sidescroll.Town;

namespace Z2Randomizer.RandomizerCore;

/// <summary>
/// Archipelago mode: the multiworld decides what every item location gives, so in the ROM they all become
/// <see cref="Collectable.AP_ITEM"/>, which gives nothing and only sets the location's collected flag.
/// That includes the P-bags, jars, small keys and extra lives the shuffle placed. Archipelago hands those out as filler.
///
/// The exceptions are the spells, sword techniques and town quest items (water, mirror, Bagu's note) when the player
/// left them out of the shuffle. The randomizer places those itself like it always does, they keep their real items
/// and they are not Archipelago locations.
/// </summary>
public static class ArchipelagoItems
{
    private static bool NeedsReplacing(Collectable collectable)
    {
        return collectable != Collectable.AP_ITEM && !collectable.IsInternalUse();
    }

    /// <summary>
    /// Whether a town's item is an Archipelago location, or is left to the randomizer because its kind of item
    /// (spells, sword techniques or town quest items) is not in the shuffle.
    /// </summary>
    public static bool IsInPool(TownMap map, Town town, RandomizerProperties props)
    {
        return map.CollectableTextType switch
        {
            CollectableTextType.WIZARD when town.Type == TownType.BAGU => props.IncludeQuestItemsInShuffle,
            CollectableTextType.WIZARD => props.IncludeSpellsInShuffle,
            CollectableTextType.DOWNSTAB_TRAINER or CollectableTextType.UPSTAB_TRAINER => props.IncludeSwordTechsInShuffle,
            CollectableTextType.TABLE or CollectableTextType.FOUNTAIN => props.IncludeQuestItemsInShuffle,
            // items lying in a town building, such as Granny's basement
            _ => true,
        };
    }

    /// <summary>
    /// The real items the randomizer places in the town spots that are not in the pool. Small items don't count.
    /// </summary>
    public static List<(Location Location, TownMap Map, Collectable Item)> FixedItems(IEnumerable<Location> locations, RandomizerProperties props)
    {
        List<(Location, TownMap, Collectable)> fixedItems = [];
        foreach (var location in locations.Where(l => l.Town != null))
        {
            foreach (var map in location.Town!.TownMaps)
            {
                if (map.Collectable is { } item && !item.IsMinorItem() && !item.IsInternalUse()
                    && !IsInPool(map, location.Town, props))
                {
                    fixedItems.Add((location, map, item));
                }
            }
        }
        return fixedItems;
    }

    /// <summary>
    /// Replaces whatever is at the given locations with the AP item, whether or not it was shuffled, except for
    /// the town items that are not in the pool. Returns the number of items replaced.
    /// </summary>
    public static int Apply(IEnumerable<Location> locations, RandomizerProperties props)
    {
        int replaced = 0;
        foreach (var location in locations)
        {
            if (location.Town != null)
            {
                foreach (var townMap in location.Town.TownMaps)
                {
                    if (townMap.Collectable is { } collectable && NeedsReplacing(collectable)
                        && IsInPool(townMap, location.Town, props))
                    {
                        townMap.Collectable = Collectable.AP_ITEM;
                        replaced++;
                    }
                }
            }
            else if (location.Palace != null)
            {
                foreach (var room in location.Palace.ItemRooms)
                {
                    if (room.Collectable is { } collectable && NeedsReplacing(collectable))
                    {
                        room.Collectable = Collectable.AP_ITEM;
                        replaced++;
                    }
                }
            }
            else
            {
                var collectables = location.GetAllCollectables();
                int count = collectables.Count(NeedsReplacing);
                if (count > 0)
                {
                    replaced += count;
                    location.SetCollectables(collectables.Select(c => NeedsReplacing(c) ? Collectable.AP_ITEM : c));
                }
            }
        }
        return replaced;
    }
}
