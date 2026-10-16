using FluentAssertions;
using Z2Randomizer.RandomizerCore;
using Z2Randomizer.RandomizerCore.Overworld;

namespace Z2Randomizer.Tests;

[TestClass]
public class ApYamlTests
{
    private static RandomizerProperties Props() => new()
    {
        Seed = "277201822",
        Flags = "RANBvv1h+/=",
        StartMagicContainers = 4,
        StartHearts = 4,
        StartGems = 6,
        NewKasutoBasementRequirement = 7,
        IncludeSpellsInShuffle = true,
        IncludeSwordTechsInShuffle = false,
        IncludeQuestItemsInShuffle = true,
        StartCandle = true,
        StartLife = true,
        StartFairy = true,
        StartWithDownstab = true,
    };

    private static ApManifest Manifest(string slot = "PlayerName") => new("277201822", "flags", slot, "d",
    [
        new ApLocation("WEST_CAVE_TROPHY", "Trophy", ApFlagKind.ItemPresence, 1552, 8, 0),
        new ApLocation("WEST_GRASS", "Grass", ApFlagKind.ItemPresence, 1555, 32, 0),
    ], []);

    private static RequirementRoutes Routes() => new("277201822", "flags", false, "d", "WEST_NORTH_PALACE", [],
    [
        new TargetAccess("WEST_GRASS", "Grass", GraphNodeKind.Location, true, false,
            [new TargetRoute(["BOOTS"], []), new TargetRoute(["HAMMER", "RAFT"], [])]),
        new TargetAccess("WEST_CAVE_TROPHY", "Trophy", GraphNodeKind.Location, true, false, [new TargetRoute([], [])]),
        new TargetAccess("EAST_GREAT_PALACE:room1", "GP", GraphNodeKind.PalaceRoom, false, true,
            [new TargetRoute(["DOWNSTAB", "GLOVE", "HAMMER", "KEY", "RAFT", "UPSTAB"], [])]),
        new TargetAccess("WEST_NEVER", "Never", GraphNodeKind.Location, false, true, []),
    ]);

    // the hash has spaces between the icons when read from the ROM
    private static string Build(RandomizerProperties props, ApManifest manifest, RequirementRoutes routes,
        IReadOnlyList<(string NodeId, Collectable Item)>? fixedItems = null) =>
        ApYamlBuilder.Build(props, manifest, routes, "A B C D E F", "5.2.4", "EAST_GREAT_PALACE:room1", fixedItems ?? []);

    [TestMethod]
    public void CurrentVersion_HasThreeNumbers()
    {
        ApYamlBuilder.CurrentVersion().Should().MatchRegex(@"^\d+\.\d+\.\d+$");
    }

    [TestMethod]
    public void Build_WritesTheExpectedFile()
    {
        string yaml = Build(Props(), Manifest(), Routes());

        yaml.Should().Be("""
            name: PlayerName
            game: Z2R
            requires:
              version: 0.6.7
            Z2R:
              rom_seed: "277201822"
              format_version: 1
              randomizer_version: "5.2.4"
              flags: "RANBvv1h+/="
              hash: "ABCDEF"
              replace_fire_with_dash: false
              start_magic_containers: 4
              start_heart_containers: 4
              crystals_required: 6
              great_palace: EAST_GREAT_PALACE:room1
              new_kasuto_basement_requirement: 7
              include_spells_in_shuffle: true
              include_sword_techs_in_shuffle: false
              include_quest_items_in_shuffle: true
              starting_items: {Candle: 1, Life Spell: 1, Fairy Spell: 1, Downstab: 1}
              priority_locations: []
              locations:
                WEST_CAVE_TROPHY: {address: 1552, mask: 8, collected: 0}
                WEST_GRASS: {address: 1555, mask: 32, collected: 0}
              events: {}
              fixed_items: {}
              rules:
                EAST_GREAT_PALACE:room1: [[DOWNSTAB, GLOVE, HAMMER, KEY, RAFT, UPSTAB]]
                WEST_CAVE_TROPHY: [[]]
                WEST_GRASS: [[BOOTS], [HAMMER, RAFT]]
                WEST_NEVER: []

            """.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void Build_WritesAnEmptyStartingItemsMap()
    {
        var props = Props();
        props.StartCandle = props.StartLife = props.StartFairy = props.StartWithDownstab = false;

        Build(props, Manifest(), Routes()).Should().Contain("  starting_items: {}\n");
    }

    [TestMethod]
    public void Build_FailsWhenALocationHasNoRule()
    {
        var routes = Routes() with { Targets = Routes().Targets.Where(t => t.Target != "WEST_GRASS").ToList() };

        var act = () => Build(Props(), Manifest(), routes);

        act.Should().Throw<ImpossibleException>().WithMessage("*WEST_GRASS*");
    }

    [TestMethod]
    public void StartingItems_FollowTheDashAndMarioOptions()
    {
        var props = Props();
        props.StartJump = true;
        props.StartFire = true;

        ApYamlBuilder.StartingItems(props).Should().Contain([Collectable.JUMP_SPELL, Collectable.FIRE_SPELL])
            .And.NotContain([Collectable.TANOOKI, Collectable.DASH_SPELL]);

        props.ReplaceFireWithDash = true;
        props.MarioMode = true;

        ApYamlBuilder.StartingItems(props).Should().Contain([Collectable.TANOOKI, Collectable.DASH_SPELL])
            .And.NotContain([Collectable.JUMP_SPELL, Collectable.FIRE_SPELL]);
    }

    [TestMethod]
    [DataRow(Collectable.CANDLE, "Candle")]
    [DataRow(Collectable.LIFE_SPELL, "Life Spell")]
    [DataRow(Collectable.DOWNSTAB, "Downstab")]
    [DataRow(Collectable.MAGIC_KEY, "Magic Key")]
    [DataRow(Collectable.BAGUS_NOTE, "Bagus Note")]
    public void ItemName_UsesTitleCaseWords(Collectable collectable, string expected)
    {
        ApYamlBuilder.ItemName(collectable).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("PlayerName", "PlayerName")]
    [DataRow("Link One", "Link One")]
    [DataRow("Link_1", "Link_1")]
    [DataRow("1234", "\"1234\"")]
    [DataRow("yes", "\"yes\"")]
    [DataRow("Null", "\"Null\"")]
    [DataRow("a: b", "\"a: b\"")]
    [DataRow("x # y", "\"x # y\"")]
    [DataRow("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [DataRow("back\\slash", "\"back\\\\slash\"")]
    [DataRow("{player}", "\"{player}\"")]
    [DataRow("-dash", "\"-dash\"")]
    [DataRow("trailing ", "\"trailing \"")]
    public void Scalar_QuotesAnythingYamlCouldMisread(string text, string expected)
    {
        ApYamlBuilder.Scalar(text).Should().Be(expected);
    }

    [TestMethod]
    public void Build_ListsFixedItemsAndRequiresARuleForEach()
    {
        var routes = Routes();
        routes.Targets.Add(new TargetAccess("WEST_TOWN_MIDO:map44", "Mido", GraphNodeKind.TownMap, false, false,
            [new TargetRoute(["FAIRY", "FOUR_CONTAINERS"], [])]));
        var fixedItems = new List<(string, Collectable)>
        {
            ("WEST_TOWN_RAURU:map36", Collectable.SHIELD_SPELL),
            ("WEST_TOWN_MIDO:map44", Collectable.DOWNSTAB),
        };

        var withoutRule = () => Build(Props(), Manifest(), Routes(), fixedItems);
        withoutRule.Should().Throw<ImpossibleException>().WithMessage("*WEST_TOWN_MIDO:map44*");

        routes.Targets.Add(new TargetAccess("WEST_TOWN_RAURU:map36", "Rauru", GraphNodeKind.TownMap, false, false, [new TargetRoute([], [])]));
        string yaml = Build(Props(), Manifest(), routes, fixedItems);

        yaml.Should().Contain("""
              fixed_items:
                WEST_TOWN_MIDO:map44: Downstab
                WEST_TOWN_RAURU:map36: Shield Spell
              rules:
            """.Replace("\r\n", "\n") + "\n");
        yaml.Should().Contain("    WEST_TOWN_MIDO:map44: [[FAIRY, FOUR_CONTAINERS]]\n");
    }

    [TestMethod]
    public void Build_ReportsEachShuffleOptionFromTheSeed()
    {
        var props = Props();
        props.IncludeSpellsInShuffle = false;
        props.IncludeSwordTechsInShuffle = true;
        props.IncludeQuestItemsInShuffle = false;

        string yaml = Build(props, Manifest(), Routes());

        yaml.Should().Contain("  include_spells_in_shuffle: false\n")
            .And.Contain("  include_sword_techs_in_shuffle: true\n")
            .And.Contain("  include_quest_items_in_shuffle: false\n");
    }

    [TestMethod]
    public void Build_WritesEventsWithoutNeedingARule()
    {
        var manifest = Manifest() with
        {
            Locations = [.. Manifest().Locations, new ApLocation("WEST_PALACE1:crystal", "Palace 1 crystal", ApFlagKind.Event, 0x078d, 0xff, 1)]
        };

        string yaml = Build(Props(), manifest, Routes());

        yaml.Should().Contain("  events:\n    WEST_PALACE1:crystal: {address: 1933, mask: 255, collected: 1}\n")
            .And.NotContain("  locations:\n    WEST_CAVE_TROPHY: {address: 1552, mask: 8, collected: 0}\n    WEST_GRASS: {address: 1555, mask: 32, collected: 0}\n    WEST_PALACE1");
    }

    [TestMethod]
    public void Build_PrioritizesThePalaceItemLocationsWhenAsked()
    {
        var manifest = Manifest() with { Locations = [.. Manifest().Locations,
            new ApLocation("EAST_PALACE:room2", "Room", ApFlagKind.ItemPresence, 1560, 4, 0)] };
        var routes = Routes();
        routes = routes with { Targets = [.. routes.Targets,
            new TargetAccess("EAST_PALACE:room2", "Room", GraphNodeKind.PalaceRoom, true, false, [new TargetRoute([], [])])] };
        var props = Props();

        Build(props, manifest, routes).Should().Contain("  priority_locations: []\n");

        props.PrioritizePalaceItems = true;

        Build(props, manifest, routes).Should().Contain("  priority_locations: [EAST_PALACE:room2]\n");
    }

    [TestMethod]
    public void Build_FailsWhenTheGreatPalaceBossHasNoRule()
    {
        var act = () => ApYamlBuilder.Build(Props(), Manifest(), Routes(), "A B C D E F", "5.2.4", "NOWHERE:boss", []);

        act.Should().Throw<ImpossibleException>().WithMessage("*NOWHERE:boss*");
    }

    private static Location PalaceLocation(LocationID id, int palaceNumber, bool withBoss = true)
    {
        var palace = new Z2Randomizer.RandomizerCore.Sidescroll.Palace.Palace(palaceNumber, true);
        var boss = new Z2Randomizer.RandomizerCore.Sidescroll.Palace.Room { Name = "boss", IsBossRoom = true };
        palace.AllRooms.Add(boss);
        if (withBoss)
        {
            palace.BossRoom = boss;
        }
        return new Location(id, 30, 5, 1, Continent.WEST) { Palace = palace };
    }

    [TestMethod]
    public void GreatPalaceBoss_FindsTheGreatPalaceWhereverItIs()
    {
        // the Great Palace is number 7 and can be at any palace location when it is in the shuffle
        Location[] locations = [PalaceLocation(LocationID.WEST_PALACE1, 3), PalaceLocation(LocationID.WEST_PALACE3, 7),
            PalaceLocation(LocationID.EAST_GREAT_PALACE, 1)];

        ApYamlBuilder.GreatPalaceBoss(locations).Should().Be("PALACE3:boss");
    }

    [TestMethod]
    public void GreatPalaceBoss_FailsWithoutAGreatPalace()
    {
        var act = () => ApYamlBuilder.GreatPalaceBoss([PalaceLocation(LocationID.WEST_PALACE1, 3)]);

        act.Should().Throw<ImpossibleException>();
    }
}
