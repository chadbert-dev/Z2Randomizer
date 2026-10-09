using FluentAssertions;
using Z2Randomizer.RandomizerCore;

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
    private static string Build(RandomizerProperties props, ApManifest manifest, RequirementRoutes routes) =>
        ApYamlBuilder.Build(props, manifest, routes, "A B C D E F", "5.2.4");

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
              version: 0.6.9
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
              starting_items: {Candle: 1, Life Spell: 1, Fairy Spell: 1, Downstab: 1}
              locations:
                WEST_CAVE_TROPHY: {address: 1552, mask: 8, collected: 0}
                WEST_GRASS: {address: 1555, mask: 32, collected: 0}
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
}
