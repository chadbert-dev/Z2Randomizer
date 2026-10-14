using FluentAssertions;
using Z2Randomizer.RandomizerCore;
using Z2Randomizer.RandomizerCore.Overworld;

namespace Tests;

[TestClass]
public class HiddenPalaceTests
{
    // The game reveals the hidden palace when the flute is played in East Hyrule on the tile two rows above it.
    // It compares Link's position with these two bytes, using the same units as the location table.
    private const int FLUTE_TRIGGER_Y = 0x8382;
    private const int FLUTE_TRIGGER_X = 0x8388;

    private static Location HiddenPalaceAt(int yRaw, int x)
    {
        return new Location(LocationID.EAST_PALACE6, yRaw, x, 0, Continent.EAST)
        {
            TerrainType = Terrain.PALACE,
        };
    }

    [TestMethod]
    [DataRow(Biome.ISLANDS, 85, 19)]
    [DataRow(Biome.VANILLALIKE, 38, 32)]
    [DataRow(Biome.CANYON, 102, 45)]
    public void UpdateHiddenPalaceSpot_PutsTheFluteTriggerTwoRowsAboveThePalace(Biome biome, int yRaw, int x)
    {
        var rom = new ROM(new byte[0x40010]);

        rom.UpdateHiddenPalaceSpot(biome, HiddenPalaceAt(yRaw, x), true);

        rom.GetByte(FLUTE_TRIGGER_Y).Should().Be((byte)(yRaw - 2));
        rom.GetByte(FLUTE_TRIGGER_X).Should().Be((byte)x);
    }

    [TestMethod]
    public void UpdateHiddenPalaceSpot_LeavesTheVanillaTriggerAloneOnTheVanillaMap()
    {
        var rom = new ROM(new byte[0x40010]);
        rom.Put(FLUTE_TRIGGER_Y, 100);
        rom.Put(FLUTE_TRIGGER_X, 45);

        rom.UpdateHiddenPalaceSpot(Biome.VANILLA, HiddenPalaceAt(102, 45), true);

        rom.GetByte(FLUTE_TRIGGER_Y).Should().Be(100);
        rom.GetByte(FLUTE_TRIGGER_X).Should().Be(45);
    }
}
