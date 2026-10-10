using FluentAssertions;
using Z2Randomizer.RandomizerCore;
using Z2Randomizer.RandomizerCore.Overworld;
using Z2Randomizer.RandomizerCore.Sidescroll.Palace;

namespace Z2Randomizer.Tests;

[TestClass]
public class ArchipelagoItemsTests
{
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

        int replaced = ArchipelagoItems.Apply([hammer, bag, key]);

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

        ArchipelagoItems.Apply([location]).Should().Be(1);

        itemRoom.Collectable.Should().Be(Collectable.AP_ITEM);
    }

    [TestMethod]
    public void Apply_IsIdempotent()
    {
        var hammer = new Location(LocationID.DM_HAMMER_CAVE, 40, 10, 1, Continent.DM);
        hammer.SetCollectables([Collectable.HAMMER]);

        ArchipelagoItems.Apply([hammer]);
        ArchipelagoItems.Apply([hammer]).Should().Be(0);
    }
}
