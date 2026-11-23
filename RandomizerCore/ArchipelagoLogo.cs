using System;

namespace Z2Randomizer.RandomizerCore;

/// <summary>
/// Archipelago mode: an AP item is drawn as one 16x16 logo made of two 8x16 sprites. Both halves live in the
/// fixed $1C00 sprite page (CHR bank $68, the same in every room), in two tile slots that item_sprites.chr leaves blank.
/// The Link Doll / 1-up tiles are not touched, so a real 1-up keeps its normal art.
/// </summary>
public static class ArchipelagoLogo
{
    /// <summary>Tile byte of the left half's sprite. Odd, so it picks the $1000 pattern table; the top tile is $fc and the bottom is $fd.</summary>
    public const byte LeftTile = 0xfd;

    /// <summary>Tile byte of the right half's sprite (top tile $c6, bottom tile $c7).</summary>
    public const byte RightTile = 0xc7;

    /// <summary>The left half of the AP logo, as 32 bytes (top tile, then bottom tile).</summary>
    public static readonly byte[] Logo = Convert.FromHexString("0002007dfdffc6380307037dfcfec60054b4ab27570707032848535727070703");

    /// <summary>
    /// The right half of the AP logo (32 bytes, top then bottom).
    /// The game draws an item's second sprite flipped horizontally, so these tiles are stored mirrored.
    /// </summary>
    public static readonly byte[] LogoRight = Convert.FromHexString("00020201000100380307037d7dffc6387cdcfb77670707037cfcfb7777070703");

    /// <summary>CHR offset (from the start of the CHR data) of tiles $fc/$fd: 0x1A000 (bank $68) + 0x3C0.</summary>
    public const int LeftHalfChrOffset = 0x1A3C0;

    /// <summary>CHR offset (from the start of the CHR data) of tiles $c6/$c7: 0x1A000 (bank $68) + 0x60.</summary>
    public const int RightHalfChrOffset = 0x1A060;

    /// <summary>
    /// Writes both halves into the fixed sprite page. Returns true if the ROM's CHR is big enough to hold them.
    /// </summary>
    public static bool Apply(byte[] rom)
    {
        // the iNES header's byte 4 is the PRG bank count; the CHR starts after the PRG
        int start = rom.Length > 16 ? 16 + rom[4] * 0x4000 : 16;
        if (start + LeftHalfChrOffset + Logo.Length > rom.Length
            || start + RightHalfChrOffset + LogoRight.Length > rom.Length)
        {
            return false;
        }
        Logo.CopyTo(rom, start + LeftHalfChrOffset);
        LogoRight.CopyTo(rom, start + RightHalfChrOffset);
        return true;
    }
}
