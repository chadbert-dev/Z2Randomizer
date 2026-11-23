using System;
using js65;

namespace Z2Randomizer.RandomizerCore;

/// <summary>
/// Archipelago mode with helpful hints: a "?" over each town NPC whose text is a helpful hint, read or not (Asm/HintMarks.s).
/// The "?" is one 8x16 sprite in tiles $fe/$ff of the fixed $1C00 sprite page (CHR bank $68, mapped in every room).
/// Every 8x16 pair of that page has a user. $fe/$ff was the "Register your name" cursor, which HintMarks.s moves to
/// the boss bar tile $c5, flipped, in palette 2 (white).
/// </summary>
public static class ArchipelagoHintMark
{
    /// <summary>Tile byte of the sprite. Odd, so it picks the $1000 pattern table; the top tile is $fe and the bottom is $ff.</summary>
    public const byte Tile = 0xff;

    /// <summary>
    /// The "?" in color 3 (both planes) in the bottom tile ($ff), with a one-pixel outline in color 1 (plane 0 only).
    /// The outline's top row is the last row of the top tile ($fe). With sprite palette 1 in towns, color 3 is white
    /// and color 1 is red.
    /// </summary>
    public static readonly byte[] Glyph = Convert.FromHexString(
        "000000000000003C0000000000000000" + "7EFF6F1E3C183C183C66060C18001800");

    /// <summary>CHR offset of tile $fe in the fixed item page, bank $68 (0x1A000 + 0x3E0).</summary>
    public const int GlyphChrOffset = 0x1A3E0;

    /// <summary>
    /// Writes the "?" into the fixed item page. Call after everything else that writes bank $68.
    /// </summary>
    public static void Apply(byte[] rom)
    {
        int start = rom.Length > 16 ? 16 + rom[4] * 0x4000 : 16;
        int at = start + GlyphChrOffset;
        if (at + Glyph.Length > rom.Length)
        {
            throw new InvalidOperationException("ROM CHR is too small for the Archipelago hint mark");
        }
        Glyph.CopyTo(rom, at);
    }

    /// <summary>Adds the draw and read hooks.</summary>
    public static void AddHooks(Assembler engine)
    {
        engine.Module().Code(Util.ReadResource("Z2Randomizer.RandomizerCore.Asm.HintMarks.s"), "hint_marks.s");
    }

    public static bool Enabled(RandomizerProperties props) =>
        props.ArchipelagoMode && props.HintMarkers && props.HelpfulHints != HelpfulHintOption.NONE;
}
