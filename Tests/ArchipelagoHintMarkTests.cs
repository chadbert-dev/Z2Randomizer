using FluentAssertions;
using Z2Randomizer.RandomizerCore;

namespace Z2Randomizer.Tests;

[TestClass]
public class ArchipelagoHintMarkTests
{
    // the ROM layout the randomizer builds: 16 PRG banks of 16 KB, then 16 CHR banks of 8 KB
    private const int PrgBanks = 16;
    private const int ChrStart = 16 + PrgBanks * 0x4000;

    private static byte[] Rom()
    {
        var rom = new byte[ChrStart + 0x20000];
        rom[4] = PrgBanks;
        return rom;
    }

    [TestMethod]
    public void Glyph_IsWhiteQuestionMark_WithColor1Outline()
    {
        ArchipelagoHintMark.Glyph.Length.Should().Be(32);
        byte[] white = [0x3C, 0x66, 0x06, 0x0C, 0x18, 0x00, 0x18, 0x00];
        // plane 1 of the bottom tile is the white "?" alone; the top tile's plane 1 is empty
        ArchipelagoHintMark.Glyph.AsSpan(24, 8).ToArray().Should().Equal(white);
        ArchipelagoHintMark.Glyph.AsSpan(8, 8).ToArray().Should().AllBeEquivalentTo((byte)0);
        // plane 0 covers the "?" plus its outline, and the outline touches every white pixel on 4 sides
        byte[] plane0 = [.. ArchipelagoHintMark.Glyph.AsSpan(0, 8).ToArray(), .. ArchipelagoHintMark.Glyph.AsSpan(16, 8).ToArray()];
        byte[] w16 = [0, 0, 0, 0, 0, 0, 0, 0, .. white, 0];
        for (int r = 0; r < 16; r++)
        {
            int ring = w16[r] | (w16[r] << 1 & 0xFF) | (w16[r] >> 1) | w16[r + 1] | (r > 0 ? w16[r - 1] : 0);
            plane0[r].Should().Be((byte)ring, $"row {r}");
        }
    }

    [TestMethod]
    public void Tile_IsTheBottomTileOfTheSpritePairStartingAtFE()
    {
        ArchipelagoHintMark.Tile.Should().Be(0xff);
        ArchipelagoHintMark.GlyphChrOffset.Should().Be(0x1A000 + (0xFE - 0xC0) * 16);
    }

    [TestMethod]
    public void HintMarksAsm_HintSize_MatchesTheHintPlaceholderSize()
    {
        string asm = Util.ReadResource("Z2Randomizer.RandomizerCore.Asm.HintMarks.s");
        var match = System.Text.RegularExpressions.Regex.Match(asm, @"^HINT_SIZE\s*=\s*(\d+)", System.Text.RegularExpressions.RegexOptions.Multiline);
        match.Success.Should().BeTrue();
        int.Parse(match.Groups[1].Value).Should().Be(CustomTexts.ARCHIPELAGO_HINT_SIZE);
    }

    [TestMethod]
    public void Enabled_OnlyWhenHintMarkersIsOn_AndHelpfulHintsAreOn()
    {
        new RandomizerConfiguration().HintMarkers.Should().BeTrue("Hint Markers is on by default");

        var props = new RandomizerProperties { ArchipelagoMode = true, HelpfulHints = HelpfulHintOption.TOWNS_SEPARATE };
        ArchipelagoHintMark.Enabled(props).Should().BeFalse();

        props.HintMarkers = true;
        ArchipelagoHintMark.Enabled(props).Should().BeTrue();

        props.HelpfulHints = HelpfulHintOption.NONE;
        ArchipelagoHintMark.Enabled(props).Should().BeFalse();
    }

    [TestMethod]
    public void Apply_WritesTheGlyphOnlyInTilesFE_FF_OfTheFixedSpritePage()
    {
        var rom = Rom();
        var before = (byte[])rom.Clone();
        ArchipelagoHintMark.Apply(rom);
        rom.AsSpan(ChrStart + 0x1A3E0, 32).SequenceEqual(ArchipelagoHintMark.Glyph).Should().BeTrue();
        for (int i = 0; i < rom.Length; i++)
        {
            if (rom[i] == before[i]) { continue; }
            int chr = i - ChrStart;
            (chr >= 0x1A3E0 && chr < 0x1A400).Should().BeTrue($"byte {i:X} changed");
        }
    }
}
