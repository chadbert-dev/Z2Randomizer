using System;
using System.Linq;
using System.Text;
using js65;

namespace Z2Randomizer.RandomizerCore;

/// <summary>
/// A block in the ROM that identifies the seed and the Archipelago slot the ROM was made for, so a client can check
/// it is connecting a ROM to the right slot. It lives at a fixed CPU address in bank 7, which is always mapped.
///
/// Layout (all text is ASCII, padded with zeros):
///   +0x00  4 bytes   "Z2AP"
///   +0x04  1 byte    layout version (currently 1)
///   +0x05  3 bytes   reserved, zero
///   +0x08  16 bytes  slot name
///   +0x18  32 bytes  seed text (cut off at 32 characters)
///   +0x38  4 bytes   hash of the seed text, little endian (the seed the generator actually used)
///   +0x3c  4 bytes   reserved, zero
/// </summary>
public static class ArchipelagoSignature
{
    /// <summary>CPU address of the block, in the fixed bank.</summary>
    public const int CpuAddress = 0xfcb0;
    /// <summary>Offset of the block in the ROM file, including the 16 byte iNES header.</summary>
    public const int FileOffset = 0x1c010 + (CpuAddress - 0xc000);

    public const int Size = 64;
    public const int Version = 1;
    public const int MaxSlotNameLength = 16;
    public const int MaxSeedLength = 32;

    private const int SlotNameOffset = 0x08;
    private const int SeedOffset = 0x18;
    private const int SeedHashOffset = 0x38;
    private static readonly byte[] Magic = "Z2AP"u8.ToArray();

    /// <summary>
    /// Why a slot name can't be used, or null if it can. Names must be 1 to 16 characters of printable ASCII.
    /// </summary>
    public static string? ValidateSlotName(string? slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
        {
            return "Enter the slot name from your Archipelago YAML.";
        }
        if (slotName.Length > MaxSlotNameLength)
        {
            return $"Slot names can be at most {MaxSlotNameLength} characters.";
        }
        if (slotName.Any(c => c < ' ' || c > '~'))
        {
            return "Slot names can only use letters, numbers, spaces and symbols found on a US keyboard.";
        }
        return null;
    }

    public static byte[] Build(string slotName, string seed, int seedHash)
    {
        if (ValidateSlotName(slotName) is { } error)
        {
            throw new UserFacingException("Invalid Slot Name", error);
        }
        byte[] block = new byte[Size];
        Magic.CopyTo(block, 0);
        block[4] = Version;
        Encoding.ASCII.GetBytes(slotName).CopyTo(block, SlotNameOffset);
        // seeds can be any text, but only ASCII fits; anything else is replaced so the length stays the same
        string asciiSeed = new string(seed.Select(c => c >= ' ' && c <= '~' ? c : '?').ToArray());
        Encoding.ASCII.GetBytes(asciiSeed[..Math.Min(asciiSeed.Length, MaxSeedLength)]).CopyTo(block, SeedOffset);
        BitConverter.GetBytes(seedHash).CopyTo(block, SeedHashOffset);
        return block;
    }

    public static (string SlotName, string Seed, int SeedHash)? Parse(ReadOnlySpan<byte> block)
    {
        if (block.Length < Size || !block[..4].SequenceEqual(Magic))
        {
            return null;
        }
        static string Text(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        return (Text(block.Slice(SlotNameOffset, MaxSlotNameLength)),
            Text(block.Slice(SeedOffset, MaxSeedLength)),
            BitConverter.ToInt32(block.Slice(SeedHashOffset, 4)));
    }

    /// <summary>Writes the block into the fixed bank at <see cref="CpuAddress"/>.</summary>
    public static void Write(Assembler asm, string slotName, string seed, int seedHash)
    {
        var a = asm.Module();
        a.Segment("PRG7");
        a.Org(CpuAddress);
        a.Byt(Build(slotName, seed, seedHash));
    }
}
