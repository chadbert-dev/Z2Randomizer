using FluentAssertions;
using Z2Randomizer.RandomizerCore;

namespace Z2Randomizer.Tests;

[TestClass]
public class ArchipelagoLogoTests
{
    // the ROM layout the randomizer builds: 16 PRG banks of 16 KB, then 16 CHR banks of 8 KB
    private const int PrgBanks = 16;
    private const int ChrStart = 16 + PrgBanks * 0x4000;
    private const int DollChrOffset = 0x0A80;
    private const int DollBanks = 13;

    private static byte[] Rom(int chrSize = 0x20000, byte doll = 0x5a)
    {
        var rom = new byte[ChrStart + chrSize];
        rom[4] = PrgBanks;
        for (int bank = 0; bank < DollBanks; bank++)
        {
            int at = ChrStart + bank * 0x2000 + DollChrOffset;
            if (at + 32 <= rom.Length)
            {
                rom.AsSpan(at, 32).Fill(doll);
            }
        }
        return rom;
    }

    [TestMethod]
    public void Apply_WritesTheLeftHalfInTiles_FC_FD_OfTheFixedSpritePage()
    {
        var rom = Rom();
        ArchipelagoLogo.Apply(rom).Should().BeTrue();
        ArchipelagoLogo.LeftHalfChrOffset.Should().Be(0x1A000 + (0xFC - 0xC0) * 16);
        rom.AsSpan(ChrStart + 0x1A3C0, 32).SequenceEqual(ArchipelagoLogo.Logo).Should().BeTrue();
    }

    [TestMethod]
    public void Apply_WritesTheRightHalfInTiles_C6_C7()
    {
        var rom = Rom();
        ArchipelagoLogo.Apply(rom);
        ArchipelagoLogo.RightHalfChrOffset.Should().Be(0x1A000 + (0xC6 - 0xC0) * 16);
        rom.AsSpan(ChrStart + 0x1A060, 32).SequenceEqual(ArchipelagoLogo.LogoRight).Should().BeTrue();
    }

    [TestMethod]
    public void Apply_LeavesTheDollAndEveryOtherByteAlone()
    {
        var rom = Rom();
        var before = (byte[])rom.Clone();
        ArchipelagoLogo.Apply(rom);
        for (int i = 0; i < rom.Length; i++)
        {
            if (rom[i] == before[i]) { continue; }
            int chr = i - ChrStart;
            bool inLeft = chr >= 0x1A3C0 && chr < 0x1A3E0;
            bool inRight = chr >= 0x1A060 && chr < 0x1A080;
            (inLeft || inRight).Should().BeTrue($"byte {i:X} changed");
        }
        for (int bank = 0; bank < DollBanks; bank++)
        {
            rom.AsSpan(ChrStart + bank * 0x2000 + DollChrOffset, 32).ToArray().Should().OnlyContain(b => b == 0x5a);
        }
    }

    [TestMethod]
    public void TilePair_IsFdAndC7()
    {
        ArchipelagoLogo.LeftTile.Should().Be(0xfd);
        ArchipelagoLogo.RightTile.Should().Be(0xc7);
    }

    [TestMethod]
    public void Apply_SmallChrWritesNothing()
    {
        var rom = Rom(chrSize: 0x2000);
        var before = (byte[])rom.Clone();
        ArchipelagoLogo.Apply(rom).Should().BeFalse();
        rom.Should().Equal(before);
    }

    [TestMethod]
    public void Apply_RunTwiceGivesTheSameRom()
    {
        var rom = Rom();
        ArchipelagoLogo.Apply(rom);
        var once = (byte[])rom.Clone();
        ArchipelagoLogo.Apply(rom);
        rom.Should().Equal(once);
    }
}
