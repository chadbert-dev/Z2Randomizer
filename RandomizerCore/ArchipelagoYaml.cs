using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Z2Randomizer.RandomizerCore.Overworld;

namespace Z2Randomizer.RandomizerCore;

/// <summary>
/// Builds the player YAML for the Archipelago world: the slot name, the seed the ROM was made from, the options that
/// affect the logic, where the game keeps the flag of every AP item location and what is needed to reach each of them.
/// </summary>
public static partial class ApYamlBuilder
{
    /// <summary>Bumped when the layout of the file changes, so the world can reject files it can't read.</summary>
    public const int FormatVersion = 1;
    public const string GameName = "Z2R";
    public const string ArchipelagoVersion = "0.6.7";

    // Item names used in starting_items
    private static readonly Collectable[] STARTING_ITEM_ORDER =
    [
        Collectable.CANDLE, Collectable.GLOVE, Collectable.RAFT, Collectable.BOOTS, Collectable.FLUTE, Collectable.CROSS,
        Collectable.HAMMER, Collectable.MAGIC_KEY,
        Collectable.TROPHY, Collectable.MEDICINE, Collectable.CHILD, Collectable.MIRROR, Collectable.WATER,
        Collectable.SHIELD_SPELL, Collectable.JUMP_SPELL, Collectable.TANOOKI, Collectable.LIFE_SPELL, Collectable.FAIRY_SPELL,
        Collectable.FIRE_SPELL, Collectable.DASH_SPELL, Collectable.REFLECT_SPELL, Collectable.SPELL_SPELL, Collectable.THUNDER_SPELL,
        Collectable.UPSTAB, Collectable.DOWNSTAB,
    ];

    /// <summary>The name of an item in the YAML, such as "Life Spell" or "Downstab".</summary>
    public static string ItemName(Collectable collectable)
    {
        return string.Join(' ', collectable.ToString().Split('_')
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    /// <summary>The items the player starts with, each once. Spells follow the dash and Mario mode options.</summary>
    public static List<Collectable> StartingItems(RandomizerProperties props)
    {
        return STARTING_ITEM_ORDER
            .Where(c => !(c == Collectable.FIRE_SPELL && props.ReplaceFireWithDash)
                && !(c == Collectable.DASH_SPELL && !props.ReplaceFireWithDash)
                && !(c == Collectable.TANOOKI && !props.MarioMode)
                && !(c == Collectable.JUMP_SPELL && props.MarioMode))
            .Where(c => props.StartsWithCollectable(c))
            .ToList();
    }

    /// <summary>The version of the randomizer, such as "5.2.4".</summary>
    public static string CurrentVersion()
    {
        var version = typeof(ApYamlBuilder).Assembly.GetName().Version!;
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    /// <param name="hash">The hash shown on the file select screen of the ROM.</param>
    /// <param name="randomizerVersion">The version of the randomizer that made the ROM.</param>
    /// <param name="greatPalaceBossId">
    /// The node id of the Great Palace's boss, such as "EAST_GREAT_PALACE:boss". It can be any palace's location when
    /// the Great Palace is in the shuffle, since the palaces move between the locations.
    /// </param>
    /// <param name="fixedItems">
    /// The items the randomizer placed itself in spots that are not Archipelago locations, because the player left
    /// their kind of item out of the shuffle. Node id and item.
    /// </param>
    /// <summary>The node id of the boss of the Great Palace, wherever the Great Palace ended up.</summary>
    public static string GreatPalaceBoss(IEnumerable<Location> locations)
    {
        foreach (var location in locations)
        {
            if (location.Palace is { Number: 7, BossRoom: { } boss } palace)
            {
                return GraphNodeIds.PalaceRoom(location, palace, boss);
            }
        }
        throw new ImpossibleException("No location holds the Great Palace");
    }

    public static string Build(RandomizerProperties props, ApManifest manifest, RequirementRoutes routes, string hash, string randomizerVersion,
        string greatPalaceBossId, IReadOnlyList<(string NodeId, Collectable Item)> fixedItems)
    {
        var rules = routes.Targets.ToDictionary(t => t.Target);
        if (!rules.ContainsKey(greatPalaceBossId))
        {
            throw new ImpossibleException($"No rule for the Great Palace boss {greatPalaceBossId}");
        }
        // a crystal is not a graph node, it is placed when the boss of the palace is beaten, so it uses the rule of the boss
        string RuleId(ApLocation l) => l.Kind == ApFlagKind.Event ? GraphNodeIds.PalaceCrystalBoss(l.NodeId) : l.NodeId;
        var missing = manifest.Locations.Select(RuleId).Concat(fixedItems.Select(f => f.NodeId))
            .Where(id => !rules.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new ImpossibleException($"No rule for AP locations: {string.Join(", ", missing)}");
        }

        var sb = new StringBuilder();
        sb.Append("name: ").Append(Scalar(manifest.SlotName)).Append('\n');
        sb.Append("game: ").Append(GameName).Append('\n');
        sb.Append("requires:\n");
        sb.Append("  version: ").Append(ArchipelagoVersion).Append('\n');
        sb.Append(GameName).Append(":\n");
        sb.Append("  rom_seed: ").Append(Quoted(props.Seed ?? "")).Append('\n');
        sb.Append("  format_version: ").Append(FormatVersion).Append('\n');
        // enough to make the same ROM again if the original file is lost
        sb.Append("  randomizer_version: ").Append(Quoted(randomizerVersion)).Append('\n');
        sb.Append("  flags: ").Append(Quoted(props.Flags ?? "")).Append('\n');
        sb.Append("  hash: ").Append(Quoted(hash.Replace(" ", ""))).Append('\n');
        sb.Append("  replace_fire_with_dash: ").Append(props.ReplaceFireWithDash ? "true" : "false").Append('\n');
        sb.Append("  start_magic_containers: ").Append(props.StartMagicContainers).Append('\n');
        sb.Append("  start_heart_containers: ").Append(props.StartHearts).Append('\n');
        sb.Append("  crystals_required: ").Append(props.StartGems).Append('\n');
        // the palaces can swap places, so this says which boss location belongs to the Great Palace
        sb.Append("  great_palace: ").Append(Key(greatPalaceBossId)).Append('\n');
        // magic containers needed before Granny's basement in New Kasuto opens
        sb.Append("  new_kasuto_basement_requirement: ").Append(props.NewKasutoBasementRequirement).Append('\n');
        // The kinds of items that are shuffled into Archipelago. For the ones that are not, the randomizer places the
        // items itself (see fixed_items) and those spots are not locations.
        sb.Append("  include_spells_in_shuffle: ").Append(props.IncludeSpellsInShuffle ? "true" : "false").Append('\n');
        sb.Append("  include_sword_techs_in_shuffle: ").Append(props.IncludeSwordTechsInShuffle ? "true" : "false").Append('\n');
        sb.Append("  include_quest_items_in_shuffle: ").Append(props.IncludeQuestItemsInShuffle ? "true" : "false").Append('\n');
        sb.Append("  starting_items: {")
            .Append(string.Join(", ", StartingItems(props).Select(c => $"{Scalar(ItemName(c))}: 1"))).Append("}\n");

        // Archipelago fills the priority locations with progression items first
        var priorityIds = props.PrioritizePalaceItems
            ? manifest.Locations.Where(l => l.Kind != ApFlagKind.Event && rules[l.NodeId].Kind == GraphNodeKind.PalaceRoom)
                .Select(l => l.NodeId).OrderBy(id => id, StringComparer.Ordinal).ToList()
            : [];
        sb.Append("  priority_locations: [").Append(string.Join(", ", priorityIds.Select(Key))).Append("]\n");

        sb.Append("  locations:\n");
        foreach (var location in manifest.Locations.OrderBy(l => l.NodeId, StringComparer.Ordinal))
        {
            sb.Append("    ").Append(Key(location.NodeId))
                .Append($": {{address: {location.Address}, mask: {location.Mask}, collected: {location.CollectedBitValue}}}\n");
        }

        // the crystals are always placed in their palace
        var allFixed = fixedItems.Select(f => (f.NodeId, Name: ItemName(f.Item)))
            .Concat(manifest.Locations.Where(l => l.FixedItem != null).Select(l => (l.NodeId, Name: l.FixedItem!)))
            .OrderBy(f => f.NodeId, StringComparer.Ordinal).ToList();
        if (allFixed.Count == 0)
        {
            sb.Append("  fixed_items: {}\n");
        }
        else
        {
            sb.Append("  fixed_items:\n");
            foreach (var (nodeId, name) in allFixed)
            {
                sb.Append("    ").Append(Key(nodeId)).Append(": ").Append(Scalar(name)).Append('\n');
            }
        }

        sb.Append("  rules:\n");
        var ruleTargets = routes.Targets.Select(t => (Id: t.Target, t.Routes))
            .Concat(manifest.Locations.Where(l => l.Kind == ApFlagKind.Event).Select(l => (Id: l.NodeId, rules[RuleId(l)].Routes)));
        foreach (var target in ruleTargets.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            // alternatives (OR) of requirements that are all needed (AND). [[]] is free, [] can't be reached.
            var alternatives = target.Routes.Select(r => "[" + string.Join(", ", r.Requirements) + "]");
            sb.Append("    ").Append(Key(target.Id)).Append(": [").Append(string.Join(", ", alternatives)).Append("]\n");
        }
        return sb.ToString();
    }

    // Words YAML would read as something other than text
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        { "true", "false", "yes", "no", "on", "off", "y", "n", "null", "~" };

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*( [A-Za-z0-9_]+)*$")]
    private static partial Regex PlainText();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_:]*$")]
    private static partial Regex PlainKey();

    /// <summary>A double quoted YAML string.</summary>
    public static string Quoted(string text)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in text)
        {
            sb.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' => $"\\x{(int)c:x2}",
                _ => c.ToString()
            });
        }
        return sb.Append('"').ToString();
    }

    /// <summary>Text as a plain YAML scalar when that reads back as the same text, else quoted.</summary>
    public static string Scalar(string text)
    {
        return PlainText().IsMatch(text) && !Reserved.Contains(text) ? text : Quoted(text);
    }

    private static string Key(string id) => PlainKey().IsMatch(id) ? id : Quoted(id);
}
