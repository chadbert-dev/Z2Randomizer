using FluentAssertions;
using Z2Randomizer.RandomizerCore;

namespace Z2Randomizer.Tests;

[TestClass]
public class ArchipelagoSignatureTests
{
    [TestMethod]
    public void Build_ThenParse_RoundTrips()
    {
        byte[] block = ArchipelagoSignature.Build("Link_Player 1", "458667598", -123456);

        block.Should().HaveCount(ArchipelagoSignature.Size);
        block[..4].Should().Equal("Z2AP"u8.ToArray());
        ArchipelagoSignature.Parse(block).Should().Be(("Link_Player 1", "458667598", -123456));
    }

    [TestMethod]
    public void Build_AllowsSixteenCharacterSlotNames()
    {
        string name = new('x', 16);

        ArchipelagoSignature.Parse(ArchipelagoSignature.Build(name, "1", 0))!.Value.SlotName.Should().Be(name);
    }

    [TestMethod]
    public void Build_CutsLongSeedsAtThirtyTwoCharacters()
    {
        string seed = new string('1', 40);

        var parsed = ArchipelagoSignature.Parse(ArchipelagoSignature.Build("slot", seed, 5))!.Value;

        parsed.Seed.Should().Be(new string('1', 32));
        parsed.SeedHash.Should().Be(5);
    }

    [TestMethod]
    public void Build_ReplacesNonAsciiSeedCharacters()
    {
        ArchipelagoSignature.Parse(ArchipelagoSignature.Build("slot", "sé€d", 0))!.Value.Seed.Should().Be("s??d");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("12345678901234567")]
    [DataRow("Zelda é")]
    [DataRow("tab\there")]
    public void ValidateSlotName_RejectsBadNames(string? name)
    {
        ArchipelagoSignature.ValidateSlotName(name).Should().NotBeNull();
        if (name != null && name.Trim().Length > 0)
        {
            var act = () => ArchipelagoSignature.Build(name, "1", 0);
            act.Should().Throw<UserFacingException>();
        }
    }

    [TestMethod]
    [DataRow("a")]
    [DataRow("Link")]
    [DataRow("Some Player-1!")]
    [DataRow("1234567890123456")]
    public void ValidateSlotName_AcceptsGoodNames(string name)
    {
        ArchipelagoSignature.ValidateSlotName(name).Should().BeNull();
    }

    [TestMethod]
    public void Parse_RejectsABlockWithoutTheMarker()
    {
        ArchipelagoSignature.Parse(new byte[ArchipelagoSignature.Size]).Should().BeNull();
        ArchipelagoSignature.Parse(new byte[10]).Should().BeNull();
    }

    [TestMethod]
    public void FileOffset_IsInsideTheFixedBank()
    {
        // bank 7 is mapped at $c000 and starts at file offset 0x1c010
        ArchipelagoSignature.FileOffset.Should().Be(0x1c010 + 0x3cb0);
        (ArchipelagoSignature.CpuAddress + ArchipelagoSignature.Size).Should().BeLessThanOrEqualTo(0xfcfb);
    }
}
