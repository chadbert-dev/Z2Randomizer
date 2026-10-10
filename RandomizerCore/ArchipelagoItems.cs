using System.Collections.Generic;
using System.Linq;
using Z2Randomizer.RandomizerCore.Overworld;

namespace Z2Randomizer.RandomizerCore;

/// <summary>
/// Archipelago mode: the multiworld decides what every item location gives, so in the ROM they all become
/// <see cref="Collectable.AP_ITEM"/>, which gives nothing and only sets the location's collected flag.
/// That includes the P-bags, jars, small keys and extra lives the shuffle placed. Archipelago hands those out as filler.
/// </summary>
public static class ArchipelagoItems
{
    private static bool NeedsReplacing(Collectable collectable)
    {
        return collectable != Collectable.AP_ITEM && !collectable.IsInternalUse();
    }

    /// <summary>
    /// Replaces whatever is at the given locations with the AP item, whether or not it was shuffled.
    /// Returns the number of items replaced.
    /// </summary>
    public static int Apply(IEnumerable<Location> locations)
    {
        int replaced = 0;
        foreach (var location in locations)
        {
            if (location.Town != null)
            {
                foreach (var townMap in location.Town.TownMaps)
                {
                    if (townMap.Collectable is { } collectable && NeedsReplacing(collectable))
                    {
                        townMap.Collectable = Collectable.AP_ITEM;
                        replaced++;
                    }
                }
            }
            else if (location.Palace != null)
            {
                foreach (var room in location.Palace.ItemRooms)
                {
                    if (room.Collectable is { } collectable && NeedsReplacing(collectable))
                    {
                        room.Collectable = Collectable.AP_ITEM;
                        replaced++;
                    }
                }
            }
            else
            {
                var collectables = location.GetAllCollectables();
                int count = collectables.Count(NeedsReplacing);
                if (count > 0)
                {
                    replaced += count;
                    location.SetCollectables(collectables.Select(c => NeedsReplacing(c) ? Collectable.AP_ITEM : c));
                }
            }
        }
        return replaced;
    }
}
