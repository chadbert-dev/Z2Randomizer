using FluentAssertions;
using Z2Randomizer.RandomizerCore;
using Z2Randomizer.RandomizerCore.Overworld;
using Z2Randomizer.RandomizerCore.Sidescroll.Palace;
using Z2Randomizer.RandomizerCore.Sidescroll.Town;

namespace Z2Randomizer.Tests;

[TestClass]
public class GraphNodeIdsTests
{
    private static Location TownLocation(LocationID id, TownType type, int map, CollectableTextType? textType)
    {
        var townMap = new TownMap(map, [4, 0, 0, 0], [1], false) { Name = "map", CollectableTextType = textType };
        var town = new Town([townMap], new(), new()) { Type = type, Name = "town" };
        return new Location(id, 30, 5, 1, Continent.WEST) { Town = town };
    }

    [TestMethod]
    [DataRow(LocationID.WEST_TOWN_RAURU, TownType.RAURU, 36, CollectableTextType.WIZARD, "WEST_TOWN_RAURU:wizard")]
    [DataRow(LocationID.WEST_TOWN_RUTO, TownType.RUTO, 37, CollectableTextType.WIZARD, "WEST_TOWN_RUTO:wizard")]
    [DataRow(LocationID.WEST_TOWN_SARIA_NORTH, TownType.SARIA, 30, CollectableTextType.TABLE, "WEST_TOWN_SARIA_NORTH:table")]
    [DataRow(LocationID.WEST_TOWN_SARIA_NORTH, TownType.SARIA, 38, CollectableTextType.WIZARD, "WEST_TOWN_SARIA_NORTH:wizard")]
    [DataRow(LocationID.WEST_TOWN_MIDO, TownType.MIDO, 44, CollectableTextType.DOWNSTAB_TRAINER, "WEST_TOWN_MIDO:downstab_man")]
    [DataRow(LocationID.WEST_TOWN_MIDO, TownType.MIDO, 39, CollectableTextType.WIZARD, "WEST_TOWN_MIDO:wizard")]
    [DataRow(LocationID.EAST_TOWN_NABOORU, TownType.NABOORU, 13, CollectableTextType.FOUNTAIN, "EAST_TOWN_NABOORU:fountain")]
    [DataRow(LocationID.EAST_TOWN_NABOORU, TownType.NABOORU, 40, CollectableTextType.WIZARD, "EAST_TOWN_NABOORU:wizard")]
    [DataRow(LocationID.EAST_TOWN_DARUNIA, TownType.DARUNIA, 45, CollectableTextType.UPSTAB_TRAINER, "EAST_TOWN_DARUNIA:upstab_man")]
    [DataRow(LocationID.EAST_TOWN_DARUNIA, TownType.DARUNIA, 41, CollectableTextType.WIZARD, "EAST_TOWN_DARUNIA:wizard")]
    [DataRow(LocationID.EAST_TOWN_NEW_KASUTO, TownType.NEW_KASUTO, 42, CollectableTextType.WIZARD, "EAST_TOWN_NEW_KASUTO:wizard")]
    [DataRow(LocationID.EAST_TOWN_OLD_KASUTO, TownType.OLD_KASUTO, 43, CollectableTextType.WIZARD, "EAST_TOWN_OLD_KASUTO:wizard")]
    [DataRow(LocationID.WEST_BAGU_HOUSE, TownType.BAGU, 31, CollectableTextType.WIZARD, "BAGU")]
    public void TownMap_IsNamedByWhoGivesTheItem(LocationID id, TownType type, int map, CollectableTextType textType, string expected)
    {
        var location = TownLocation(id, type, map, textType);

        GraphNodeIds.TownMap(location, location.Town!.TownMaps[0]).Should().Be(expected);
    }

    [TestMethod]
    [DataRow((int)VanillaTownMap.GRANNYS_BASEMENT, "EAST_TOWN_NEW_KASUTO:ganon_granny")]
    [DataRow((int)VanillaTownMap.SPELL_TOWER_INTERIOR, "EAST_TOWN_NEW_KASUTO:spell_tower")]
    [DataRow(20, "EAST_TOWN_NEW_KASUTO:map20")]
    public void TownMap_NamesTheNewKasutoBuildings(int map, string expected)
    {
        var location = TownLocation(LocationID.EAST_TOWN_NEW_KASUTO, TownType.NEW_KASUTO, map, null);

        GraphNodeIds.TownMap(location, location.Town!.TownMaps[0]).Should().Be(expected);
    }

    [TestMethod]
    public void TownMap_KeepsMapNumbersForMapsThatHoldNoItem()
    {
        var location = TownLocation(LocationID.WEST_TOWN_RAURU, TownType.RAURU, 35, null);

        GraphNodeIds.TownMap(location, location.Town!.TownMaps[0]).Should().Be("WEST_TOWN_RAURU:map35");
    }

    private static (Location, Palace, Room boss, Room[] items, Room other) PalaceWith(int itemRooms)
    {
        var palace = new Palace(1, true);
        var other = new Room { Name = "other" };
        var boss = new Room { Name = "boss", IsBossRoom = true };
        var items = Enumerable.Range(0, itemRooms).Select(i => new Room { Name = $"item{i}", HasItem = true }).ToArray();
        palace.AllRooms.AddRange([other, boss, .. items]);
        palace.ItemRooms.AddRange(items);
        palace.BossRoom = boss;
        return (new Location(LocationID.WEST_PALACE1, 30, 5, 1, Continent.WEST) { Palace = palace }, palace, boss, items, other);
    }

    [TestMethod]
    public void PalaceRoom_NamesTheBossRoom()
    {
        var (location, palace, boss, _, _) = PalaceWith(1);

        GraphNodeIds.PalaceRoom(location, palace, boss).Should().Be("PALACE1:boss");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void PalaceRoom_NumbersItemRoomsFromOne(int itemRooms)
    {
        var (location, palace, _, items, _) = PalaceWith(itemRooms);

        items.Select(r => GraphNodeIds.PalaceRoom(location, palace, r)).Should()
            .Equal(Enumerable.Range(1, itemRooms).Select(i => $"PALACE1:item{i}"));
    }

    [TestMethod]
    public void PalaceRoom_KeepsRoomIndexesForEveryOtherRoom()
    {
        var (location, palace, _, _, other) = PalaceWith(2);

        GraphNodeIds.PalaceRoom(location, palace, other).Should().Be("PALACE1:room0");
    }

    [TestMethod]
    public void PalaceRoom_CountsABossRoomThatHoldsAnItemAsTheBossRoom()
    {
        var (location, palace, boss, _, _) = PalaceWith(1);
        palace.ItemRooms.Add(boss);

        GraphNodeIds.PalaceRoom(location, palace, boss).Should().Be("PALACE1:boss");
    }

    [TestMethod]
    public void PalaceRoom_IsNamedByThePalaceNumberNotTheLocation()
    {
        // palaces swap places, so palace 5 can sit at the location of palace 1
        var (location, _, _, _, _) = PalaceWith(1);
        var palace = new Palace(5, true);
        var boss = new Room { Name = "boss", IsBossRoom = true };
        palace.AllRooms.Add(boss);
        palace.BossRoom = boss;
        location.Palace = palace;

        GraphNodeIds.PalaceRoom(location, palace, boss).Should().Be("PALACE5:boss");
        GraphNodeIds.PalaceCrystal(location).Should().Be("PALACE5:crystal");
    }

    [TestMethod]
    public void PalaceRoom_NamesTheGreatPalaceWhereverItIs()
    {
        var (location, _, _, _, _) = PalaceWith(1);
        var palace = new Palace(7, true);
        var boss = new Room { Name = "boss", IsBossRoom = true };
        palace.AllRooms.Add(boss);
        palace.BossRoom = boss;
        location.Palace = palace;

        GraphNodeIds.PalaceRoom(location, palace, boss).Should().Be("GREAT_PALACE:boss");
    }
}
