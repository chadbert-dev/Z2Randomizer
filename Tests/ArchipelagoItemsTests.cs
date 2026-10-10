using FluentAssertions;
using Z2Randomizer.RandomizerCore;
using Z2Randomizer.RandomizerCore.Overworld;
using Z2Randomizer.RandomizerCore.Sidescroll.Palace;
using Z2Randomizer.RandomizerCore.Sidescroll.Town;

namespace Z2Randomizer.Tests;

[TestClass]
public class ArchipelagoItemsTests
{
    private static RandomizerProperties AllInShuffle() => new()
    {
        IncludeSpellsInShuffle = true,
        IncludeSwordTechsInShuffle = true,
        IncludeQuestItemsInShuffle = true,
    };

    private static RandomizerProperties NothingInShuffle() => new();

    private static Location TownWith(LocationID id, TownType type, CollectableTextType textType, Collectable item)
    {
        var townMap = new TownMap(40, [4, 0, 0, 0], [1], false) { Name = "map", Collectable = item, CollectableTextType = textType };
        var town = new Town([townMap], new(), new()) { Type = type, Name = "town" };
        return new Location(id, 30, 5, 1, Continent.WEST) { Town = town };
    }

    [TestMethod]
    public void ApItem_IsNeitherMinorNorRequired()
    {
        Collectable.AP_ITEM.IsMinorItem().Should().BeFalse();
        Collectable.AP_ITEM.IsItemGetItem().Should().BeFalse();
        Collectable.AP_ITEM.AsRequirement().Should().BeNull();
        Collectable.AP_ITEM.GetROMCollectableIndex().Should().Be(0x26);
        Collectable.AP_ITEM.EnglishText().Should().NotBe("MISSING");
        Collectable.AP_ITEM.SingleLineText().Should().NotBe("MISSING");
    }

    [TestMethod]
    public void Apply_ReplacesEveryItemInOverworldLocations_IncludingSmallItems()
    {
        var hammer = new Location(LocationID.DM_HAMMER_CAVE, 40, 10, 1, Continent.DM);
        hammer.SetCollectables([Collectable.HAMMER]);
        var bag = new Location(LocationID.WEST_CAVE_PBAG, 41, 11, 1, Continent.WEST);
        bag.SetCollectables([Collectable.LARGE_BAG]);
        var key = new Location(LocationID.WEST_CAVE_TROPHY, 42, 12, 1, Continent.WEST);
        key.SetCollectables([Collectable.KEY]);

        int replaced = ArchipelagoItems.Apply([hammer, bag, key], AllInShuffle());

        replaced.Should().Be(3);
        hammer.GetAllCollectables().Should().Equal(Collectable.AP_ITEM);
        bag.GetAllCollectables().Should().Equal(Collectable.AP_ITEM);
        key.GetAllCollectables().Should().Equal(Collectable.AP_ITEM);
    }

    [TestMethod]
    [DataRow(Collectable.SMALL_BAG)]
    [DataRow(Collectable.XL_BAG)]
    [DataRow(Collectable.RED_JAR)]
    [DataRow(Collectable.BLUE_JAR)]
    [DataRow(Collectable.KEY)]
    [DataRow(Collectable.ONEUP)]
    [DataRow(Collectable.CANDLE)]
    [DataRow(Collectable.HEART_CONTAINER)]
    [DataRow(Collectable.THUNDER_SPELL)]
    public void Apply_ReplacesEveryKindOfItemInPalaceItemRooms(Collectable placed)
    {
        var itemRoom = new Room { HasItem = true, Collectable = placed };
        var palace = new Palace(1, true);
        palace.ItemRooms.Add(itemRoom);
        var location = new Location(LocationID.WEST_PALACE1, 30, 5, 1, Continent.WEST) { Palace = palace };

        ArchipelagoItems.Apply([location], AllInShuffle()).Should().Be(1);

        itemRoom.Collectable.Should().Be(Collectable.AP_ITEM);
    }

    [TestMethod]
    public void Apply_IsIdempotent()
    {
        var hammer = new Location(LocationID.DM_HAMMER_CAVE, 40, 10, 1, Continent.DM);
        hammer.SetCollectables([Collectable.HAMMER]);

        ArchipelagoItems.Apply([hammer], AllInShuffle());
        ArchipelagoItems.Apply([hammer], AllInShuffle()).Should().Be(0);
    }

    // wizards are spells, the trainers are sword techniques, and Bagu, the mirror table and the fountain are quest items
    private static readonly (TownType Town, CollectableTextType Text, Collectable Item, string Option)[] ShuffleCategories =
    [
        (TownType.RAURU, CollectableTextType.WIZARD, Collectable.SHIELD_SPELL, "spells"),
        (TownType.MIDO, CollectableTextType.DOWNSTAB_TRAINER, Collectable.DOWNSTAB, "techs"),
        (TownType.DARUNIA, CollectableTextType.UPSTAB_TRAINER, Collectable.UPSTAB, "techs"),
        (TownType.BAGU, CollectableTextType.WIZARD, Collectable.BAGUS_NOTE, "quest"),
        (TownType.SARIA, CollectableTextType.TABLE, Collectable.MIRROR, "quest"),
        (TownType.NABOORU, CollectableTextType.FOUNTAIN, Collectable.WATER, "quest"),
    ];

    private static RandomizerProperties OnlyIn(string option) => new()
    {
        IncludeSpellsInShuffle = option == "spells",
        IncludeSwordTechsInShuffle = option == "techs",
        IncludeQuestItemsInShuffle = option == "quest",
    };

    [TestMethod]
    public void Apply_ReplacesTownItemsOnlyWhenTheirKindIsInTheShuffle()
    {
        foreach (var (town, text, item, option) in ShuffleCategories)
        {
            foreach (var enabled in new[] { true, false })
            {
                var location = TownWith(LocationID.WEST_TOWN_RAURU, town, text, item);
                var props = enabled ? OnlyIn(option) : NothingInShuffle();

                ArchipelagoItems.Apply([location], props);

                location.Town!.TownMaps[0].Collectable.Should().Be(enabled ? Collectable.AP_ITEM : item, $"{item} with {option} {(enabled ? "in" : "out of")} the shuffle");
            }
        }
    }

    [TestMethod]
    public void Apply_DoesNotLetOneKindOfOptionSaveAnother()
    {
        // only spells in the shuffle: the sword techniques and quest items stay put
        foreach (var (town, text, item, option) in ShuffleCategories.Where(c => c.Option != "spells"))
        {
            var location = TownWith(LocationID.WEST_TOWN_RAURU, town, text, item);

            ArchipelagoItems.Apply([location], OnlyIn("spells"));

            location.Town!.TownMaps[0].Collectable.Should().Be(item);
        }
    }

    [TestMethod]
    public void Apply_AlwaysReplacesItemsInTownBuildings()
    {
        var location = TownWith(LocationID.EAST_TOWN_NEW_KASUTO, TownType.NEW_KASUTO, default, Collectable.MAGIC_CONTAINER);
        location.Town!.TownMaps[0].CollectableTextType = null;

        ArchipelagoItems.Apply([location], NothingInShuffle());

        location.Town.TownMaps[0].Collectable.Should().Be(Collectable.AP_ITEM);
    }

    [TestMethod]
    public void FixedItems_ListsTheRealItemsOfSpotsThatAreNotInThePool()
    {
        var wizard = TownWith(LocationID.WEST_TOWN_RAURU, TownType.RAURU, CollectableTextType.WIZARD, Collectable.SHIELD_SPELL);
        var trainer = TownWith(LocationID.WEST_TOWN_MIDO, TownType.MIDO, CollectableTextType.DOWNSTAB_TRAINER, Collectable.DOWNSTAB);

        var fixedItems = ArchipelagoItems.FixedItems([wizard, trainer], OnlyIn("techs"));

        fixedItems.Select(f => f.Item).Should().Equal(Collectable.SHIELD_SPELL);
        fixedItems.Single().Location.Should().BeSameAs(wizard);
    }

    [TestMethod]
    public void FixedItems_LeavesOutSmallItemsAndPoolSpots()
    {
        // a wizard that holds a P-bag because the player starts with the spell
        var wizard = TownWith(LocationID.WEST_TOWN_RAURU, TownType.RAURU, CollectableTextType.WIZARD, Collectable.LARGE_BAG);

        ArchipelagoItems.FixedItems([wizard], NothingInShuffle()).Should().BeEmpty();
        ArchipelagoItems.FixedItems([wizard], AllInShuffle()).Should().BeEmpty();
    }
}
