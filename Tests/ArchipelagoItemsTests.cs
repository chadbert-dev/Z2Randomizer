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
        Collectable.AP_ITEM.IsMajorItem().Should().BeFalse();
        Collectable.AP_ITEM.GetROMCollectableIndex().Should().Be(0x26);
        Collectable.AP_ITEM.EnglishText().Should().NotBe("MISSING");
        Collectable.AP_ITEM.SingleLineText().Should().NotBe("MISSING");
    }

    [TestMethod]
    public void IsMajorItem_ExcludesBagsJarsKeysAndOneUps()
    {
        Collectable[] minor = [Collectable.SMALL_BAG, Collectable.MEDIUM_BAG, Collectable.LARGE_BAG, Collectable.XL_BAG,
            Collectable.RED_JAR, Collectable.BLUE_JAR, Collectable.KEY, Collectable.ONEUP];
        Collectable[] major = [Collectable.CANDLE, Collectable.HAMMER, Collectable.MAGIC_KEY, Collectable.HEART_CONTAINER,
            Collectable.MAGIC_CONTAINER, Collectable.TROPHY, Collectable.CHILD, Collectable.MIRROR, Collectable.BAGUS_NOTE,
            Collectable.UPSTAB, Collectable.DOWNSTAB, Collectable.SHIELD_SPELL, Collectable.DASH_SPELL, Collectable.TANOOKI];

        minor.Should().OnlyContain(c => !c.IsMajorItem());
        major.Should().OnlyContain(c => c.IsMajorItem());
        Collectable.DO_NOT_USE.IsMajorItem().Should().BeFalse();
        Collectable.DO_NOT_USE_ANTIFAIRY.IsMajorItem().Should().BeFalse();
    }

    [TestMethod]
    public void Apply_ReplacesMajorItemsInOverworldLocationsOnly()
    {
        var hammer = new Location(LocationID.DM_HAMMER_CAVE, 40, 10, 1, Continent.DM);
        hammer.SetCollectables([Collectable.HAMMER]);
        var bag = new Location(LocationID.WEST_CAVE_PBAG, 41, 11, 1, Continent.WEST);
        bag.SetCollectables([Collectable.LARGE_BAG]);

        int replaced = ArchipelagoItems.Apply([hammer, bag]);

        replaced.Should().Be(1);
        hammer.GetAllCollectables().Should().Equal(Collectable.AP_ITEM);
        bag.GetAllCollectables().Should().Equal(Collectable.LARGE_BAG);
    }

    [TestMethod]
    public void Apply_ReplacesMajorItemsInPalaceItemRooms()
    {
        var itemRoom1 = new Room { HasItem = true, Collectable = Collectable.CANDLE };
        var itemRoom2 = new Room { HasItem = true, Collectable = Collectable.KEY };
        var palace = new Palace(1, true);
        palace.ItemRooms.AddRange([itemRoom1, itemRoom2]);
        var location = new Location(LocationID.WEST_PALACE1, 30, 5, 1, Continent.WEST) { Palace = palace };

        int replaced = ArchipelagoItems.Apply([location]);

        replaced.Should().Be(1);
        itemRoom1.Collectable.Should().Be(Collectable.AP_ITEM);
        itemRoom2.Collectable.Should().Be(Collectable.KEY);
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
