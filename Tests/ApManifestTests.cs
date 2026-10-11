using System.Text.Json;
using FluentAssertions;
using Z2Randomizer.RandomizerCore;
using Z2Randomizer.RandomizerCore.Overworld;
using Z2Randomizer.RandomizerCore.Sidescroll.Palace;
using Z2Randomizer.RandomizerCore.Sidescroll.Town;

namespace Z2Randomizer.Tests;

[TestClass]
public class ApManifestTests
{
    [TestMethod]
    [DataRow(0x0600, 0, 0x0600)]
    [DataRow(0x0600, 1, 0x0600)]
    [DataRow(0x0620, 14, 0x0627)]
    [DataRow(0x0680, 5, 0x0682)]
    [DataRow(0x06a0, 63, 0x06bf)]
    public void ItemPresenceAddress_UsesTwoMapsPerByte(int tableBase, int map, int expected)
    {
        ApManifestBuilder.ItemPresenceAddress(tableBase, map).Should().Be(expected);
    }

    [TestMethod]
    public void ItemPresenceMask_EvenMapsUseHighNibbleAndOddMapsLowNibble()
    {
        int[] even = [0x80, 0x40, 0x20, 0x10];
        int[] odd = [0x08, 0x04, 0x02, 0x01];
        for (int page = 0; page < 4; page++)
        {
            ApManifestBuilder.ItemPresenceMask(6, page).Should().Be(even[page]);
            ApManifestBuilder.ItemPresenceMask(7, page).Should().Be(odd[page]);
        }
    }

    [TestMethod]
    public void ItemPresenceMask_RejectsScreensWithNoBit()
    {
        var act = () => ApManifestBuilder.ItemPresenceMask(1, 4);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static RandomizerProperties Props() => new() { Seed = "1", Flags = "f" };

    [TestMethod]
    public void Build_PalaceItemRoom_UsesRoomMapAndItemScreen()
    {
        // header, an x skip to screen 2, then an item at y=5
        byte[] sideview = [9, 0, 0, 0, 0xE2, 0x00, 0x50, 0x0F, (byte)Collectable.CANDLE];
        var room = new Room { Name = "item", HasItem = true, Map = 5, SideView = sideview, Collectable = Collectable.AP_ITEM };
        var palace = new Palace(1, true);
        palace.AllRooms.Add(room);
        palace.ItemRooms.Add(room);
        var location = new Location(LocationID.WEST_PALACE1, 30, 5, 1, Continent.WEST) { Palace = palace };

        var manifest = ApManifestBuilder.Build([], [location], null!, Props());

        manifest.Locations.Should().ContainSingle().Which.Should().Be(new ApLocation(
            "WEST_PALACE1:item1", "WEST_PALACE1 item room", ApFlagKind.ItemPresence, 0x0682, 0x02, 0));
    }

    [TestMethod]
    public void Build_SkipsItemRoomsWithoutAnApItem()
    {
        var room = new Room { HasItem = true, Map = 5, SideView = [7, 0, 0, 0, 0x50, 0x0F, (byte)Collectable.CANDLE], Collectable = Collectable.LARGE_BAG };
        var palace = new Palace(2, true);
        palace.AllRooms.Add(room);
        palace.ItemRooms.Add(room);
        var location = new Location(LocationID.WEST_PALACE2, 30, 5, 1, Continent.WEST) { Palace = palace };

        ApManifestBuilder.Build([], [location], null!, Props()).Locations.Should().BeEmpty();
    }

    private static Location TownWith(LocationID id, TownType type, int map, CollectableTextType textType)
    {
        var townMap = new TownMap(map, [4, 0, 0, 0], [1], false)
        {
            Name = "map",
            Collectable = Collectable.AP_ITEM,
            CollectableTextType = textType,
        };
        var town = new Town([townMap], new(), new()) { Type = type, Name = "town" };
        return new Location(id, 30, 5, 1, Continent.WEST) { Town = town };
    }

    [TestMethod]
    [DataRow(TownType.RAURU, 0x08)]
    [DataRow(TownType.MIDO, 0x01)]
    [DataRow(TownType.NABOORU, 0x80)]
    [DataRow(TownType.OLD_KASUTO, 0x10)]
    public void Build_WizardsUseTheirTownsBitInTheWizardFlags(TownType type, int expectedMask)
    {
        var location = TownWith(LocationID.WEST_TOWN_RAURU, type, 36, CollectableTextType.WIZARD);

        var entry = ApManifestBuilder.Build([], [location], null!, Props()).Locations.Should().ContainSingle().Subject;

        entry.Kind.Should().Be(ApFlagKind.Wizard);
        entry.Address.Should().Be(ApManifestBuilder.WIZARD_FLAGS);
        entry.Mask.Should().Be(expectedMask);
        entry.CollectedBitValue.Should().Be(1);
    }

    [TestMethod]
    [DataRow(TownType.MIDO, CollectableTextType.DOWNSTAB_TRAINER, 0x08)]
    [DataRow(TownType.DARUNIA, CollectableTextType.UPSTAB_TRAINER, 0x04)]
    [DataRow(TownType.SARIA, CollectableTextType.TABLE, 0x02)]
    [DataRow(TownType.BAGU, CollectableTextType.WIZARD, 0x01)]
    [DataRow(TownType.NABOORU, CollectableTextType.FOUNTAIN, 0x80)]
    public void Build_TrainersAndQuestItemsUseTheMiscFlags(TownType type, CollectableTextType textType, int expectedMask)
    {
        var location = TownWith(LocationID.WEST_TOWN_MIDO, type, 44, textType);

        var entry = ApManifestBuilder.Build([], [location], null!, Props()).Locations.Should().ContainSingle().Subject;

        entry.Kind.Should().Be(ApFlagKind.Misc);
        entry.Address.Should().Be(ApManifestBuilder.MISC_FLAGS);
        entry.Mask.Should().Be(expectedMask);
    }

    [TestMethod]
    public void Build_WarnsWhenLocationsShareAFlag()
    {
        var a = TownWith(LocationID.WEST_TOWN_MIDO, TownType.MIDO, 44, CollectableTextType.DOWNSTAB_TRAINER);
        var b = TownWith(LocationID.EAST_TOWN_DARUNIA, TownType.MIDO, 45, CollectableTextType.DOWNSTAB_TRAINER);

        var manifest = ApManifestBuilder.Build([], [a, b], null!, Props());

        manifest.Warnings.Should().ContainSingle();
    }

    [TestMethod]
    public void Serialization_RoundTrips()
    {
        var manifest = new ApManifest("1", "f", "slot", "d", [new ApLocation("a", "A", ApFlagKind.Wizard, 0x77a, 8, 1)], ["w"]);

        string json = JsonSerializer.Serialize(manifest, ApManifestSerializationContext.Default.ApManifest);
        var parsed = JsonSerializer.Deserialize(json, ApManifestSerializationContext.Default.ApManifest)!;

        json.Should().Contain("\"kind\": \"Wizard\"").And.Contain("\"collectedBitValue\": 1");
        parsed.Locations.Should().Equal(manifest.Locations);
    }
}
