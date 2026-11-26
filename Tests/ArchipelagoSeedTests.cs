using System.Security.Cryptography;
using Desktop.Common;
using FluentAssertions;
using Z2Randomizer.RandomizerCore;
using Z2Randomizer.RandomizerCore.Sidescroll.Palace;

namespace Z2Randomizer.Tests;

/// <summary>
/// Builds whole seeds, one in Archipelago mode and one without, and checks the bytes. These need the vanilla
/// Zelda II ROM, which is not in the repo: set Z2R_VANILLA_ROM to its path, or the tests report inconclusive.
/// </summary>
[TestClass]
public class ArchipelagoSeedTests
{
    private const string Flags = "RAABvvyhAsnA3hTamjSnFbDdHW+#5yqsvMVs1FHyAAnADtA";
    private const string Seed = "12345";
    private const string SlotName = "Tester";

    // SHA-256 of the ROMs these tests build. The non-AP hash is the ROM from origin/ap cbf42700 (AP work must not
    // change non-AP seeds); the AP hash is this branch with hint marks, short spell names, the crystal fanfare wait and item times. A change that alters them changes every seed made
    // with these settings: for an Archipelago seed that breaks a multiworld already generated from the old ROM. If
    // the change is meant to alter the ROM, say so in the commit and update the hash.
    private const string NonApRomSha256 = "CCDC037FDA36A900FD753850E2D6C9EC93C54DE2432CFAE1668C1E48CC249BC1";
    private const string ApRomSha256 = "FB91D585B0E25791C79D1F35A3D168BE3C1868A95068E6F726CB235002926185";

    // PRG bank 4 $9B47, where the crystal statue waits for the fanfare with "ldy $07fb"
    private const int CrystalFanfareWait = 16 + 4 * 0x4000 + (0x9B47 - 0x8000);

    // PRG bank 4 $9B27, where vanilla sets the crystal placed flag with "sta $078c,y"
    private const int CrystalFlagStore = 16 + 4 * 0x4000 + (0x9B27 - 0x8000);

    private static byte[]? vanilla;
    private static (byte[] Rom, Hyrule Hyrule)? nonAp;
    private static (byte[] Rom, Hyrule Hyrule)? ap;

    [ClassInitialize]
    public static async Task Build(TestContext _)
    {
        var path = Environment.GetEnvironmentVariable("Z2R_VANILLA_ROM");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }
        vanilla = await File.ReadAllBytesAsync(path);
        nonAp = await BuildSeed(archipelago: false);
        ap = await BuildSeed(archipelago: true);
    }

    private static async Task<(byte[] Rom, Hyrule Hyrule)> BuildSeed(bool archipelago)
    {
        var config = new RandomizerConfiguration(Flags) { Seed = Seed };
        if (archipelago)
        {
            config.ArchipelagoMode = true;
            config.ArchipelagoSlotName = SlotName;
        }
        var rooms = new PalaceRooms(File.ReadAllText("PalaceRooms.json"), false);
        var hyrule = new Hyrule((options, debug) => new DesktopJsEngine(options, debug), rooms);
        var result = await hyrule.Randomize((byte[])vanilla!.Clone(), config, _ => Task.CompletedTask, CancellationToken.None);
        result.success.Should().BeTrue(result.messages);
        return (result.romdata!, hyrule);
    }

    private static (byte[] Rom, Hyrule Hyrule) Built(bool archipelago)
    {
        var seed = archipelago ? ap : nonAp;
        if (seed is null)
        {
            Assert.Inconclusive("Set Z2R_VANILLA_ROM to the path of the vanilla Zelda II ROM to run the seed tests.");
        }
        return seed!.Value;
    }

    private static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    [TestMethod]
    public void NonApSeed_IsByteIdenticalToThePinnedRom()
    {
        Sha256(Built(archipelago: false).Rom).Should().Be(NonApRomSha256);
    }

    [TestMethod]
    public void ApSeed_IsByteIdenticalToThePinnedRom()
    {
        Sha256(Built(archipelago: true).Rom).Should().Be(ApRomSha256);
    }

    [TestMethod]
    public void NonApSeed_HasNoArchipelagoTextOrSlots()
    {
        var (rom, hyrule) = Built(archipelago: false);
        hyrule.Props.ArchipelagoHintSlots.Should().BeEmpty();
        hyrule.Props.ArchipelagoSpellHintSlots.Should().BeEmpty();
        hyrule.Props.ArchipelagoItemTextSlots.Should().BeEmpty();
        foreach (var text in new[] { "AP HINT", "AP ITEM", "AP$ITEM$town" })
        {
            Contains(rom, Util.ToGameText(text, false)).Should().BeFalse($"\"{text}\" is Archipelago text");
        }
    }

    [TestMethod]
    public void ApSeed_EveryItemTextSlotHoldsItsPlaceholder()
    {
        var (rom, hyrule) = Built(archipelago: true);
        var slots = hyrule.Props.ArchipelagoItemTextSlots;
        slots.Should().NotBeEmpty();
        slots.Values.Select(s => s.Number).Should().BeEquivalentTo(Enumerable.Range(0, slots.Count));
        foreach (var (node, (index, number)) in slots)
        {
            var placeholder = CustomTexts.ArchipelagoItemTextPlaceholder(number).EncodedText;
            TextAt(rom, index, placeholder.Length).Should().Equal(placeholder, node);
        }
    }

    [TestMethod]
    public void ApSeed_EverySpellHintSlotHoldsItsPlaceholder()
    {
        var (rom, hyrule) = Built(archipelago: true);
        foreach (var (item, index) in hyrule.Props.ArchipelagoSpellHintSlots)
        {
            var placeholder = CustomTexts.ArchipelagoSpellHintPlaceholder(item).EncodedText;
            TextAt(rom, index, placeholder.Length).Should().Equal(placeholder, item.ToString());
        }
    }

    [TestMethod]
    public void ApSeed_EveryHintSlotHoldsItsPlaceholder()
    {
        var (rom, hyrule) = Built(archipelago: true);
        var slots = hyrule.Props.ArchipelagoHintSlots;
        for (int slot = 0; slot < slots.Count; slot++)
        {
            foreach (int index in slots[slot])
            {
                var placeholder = CustomTexts.ArchipelagoHintPlaceholder(slot).EncodedText;
                TextAt(rom, index, placeholder.Length).Should().Equal(placeholder, $"hint {slot}");
            }
        }
    }

    [TestMethod]
    public void ApSeed_YamlListsTheSameItemTextSlots()
    {
        var (_, hyrule) = Built(archipelago: true);
        var yaml = hyrule.GenerateArchipelagoYaml();
        foreach (var (_, (index, number)) in hyrule.Props.ArchipelagoItemTextSlots)
        {
            yaml.Should().Contain($": [{index}, {number}]");
        }
    }

    // The randomizer's HelmetHeadGoomaFix (Hyrule.cs) removes the vanilla crystal flag store, and
    // SaveTimestampForPalace (StatTracking.s) sets the flag by the post-shuffle palace number instead. If the store
    // came back, it would write the flag at the vanilla index too.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Seed_RemovesTheVanillaCrystalFlagStore(bool archipelago)
    {
        var (rom, _) = Built(archipelago);
        vanilla!.AsSpan(CrystalFlagStore, 3).ToArray().Should().Equal(0x99, 0x8c, 0x07);
        rom.AsSpan(CrystalFlagStore, 3).ToArray().Should().Equal(0xea, 0xea, 0xea);
    }

    // The client requests music while the game runs, so an Archipelago seed waits for the crystal fanfare ($40) only,
    // not for all music to stop (Asm/CrystalFanfare.s)
    [TestMethod]
    public void ApSeed_WaitsOnlyForTheCrystalFanfare()
    {
        var (rom, _) = Built(archipelago: true);
        rom[CrystalFanfareWait].Should().Be(0x20); // jsr
        int target = rom[CrystalFanfareWait + 1] | rom[CrystalFanfareWait + 2] << 8;
        // the routine is relocated into bank 4 or the fixed bank
        int bank = target >= 0xC000 ? 7 : 4;
        // lda $07fb, and #$40, tay, rts
        rom.AsSpan(16 + bank * 0x4000 + (target & 0x3FFF), 7).ToArray()
            .Should().Equal(0xad, 0xfb, 0x07, 0x29, 0x40, 0xa8, 0x60);
    }

    [TestMethod]
    public void NonApSeed_KeepsTheVanillaCrystalFanfareWait()
    {
        var (rom, _) = Built(archipelago: false);
        rom.AsSpan(CrystalFanfareWait, 3).ToArray().Should().Equal(0xac, 0xfb, 0x07);
    }

    // The client flags the items it gives in a mailbox that the gameplay NMI reads (Asm/StatTracking.s)
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Seed_ReadsTheItemTimeMailboxOnlyWhenArchipelago(bool archipelago)
    {
        var (rom, _) = Built(archipelago);
        int FixedBank(int cpu) => 16 + 7 * 0x4000 + (cpu - 0xc000);
        rom[FixedBank(0xc07a)].Should().Be(0x20); // jsr
        int target = rom[FixedBank(0xc07b)] | rom[FixedBank(0xc07c)] << 8;
        target.Should().BeGreaterThanOrEqualTo(0xc000);
        // lda $01bc
        bool readsMailbox = rom.AsSpan(FixedBank(target), 3).SequenceEqual(new byte[] { 0xad, 0xbc, 0x01 });
        readsMailbox.Should().Be(archipelago);
    }

    // The bytes of a text, found the way the Archipelago client (worlds/z2r/Client.py) finds them: the west and east
    // text tables are anywhere in bank 3, and their pointers are at CPU $B423 and $B425 of bank 3.
    private const int Bank3 = 16 + 3 * 0x4000;
    private const int WestTextCount = 52;

    private static byte[] TextAt(byte[] rom, int index, int length)
    {
        int Pointer(int at) => Bank3 + (rom[at] | rom[at + 1] << 8) - 0x8000;
        int table = index < WestTextCount ? Pointer(Bank3 + 0x3423) : Pointer(Bank3 + 0x3425);
        int entry = table + (index < WestTextCount ? index : index - WestTextCount) * 2;
        return rom.AsSpan(Pointer(entry), length).ToArray();
    }

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
