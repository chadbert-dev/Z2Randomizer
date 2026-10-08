using System.Collections.Generic;
using System.Linq;
using Z2Randomizer.RandomizerCore.Overworld;

namespace Z2Randomizer.RandomizerCore;

/// <summary>
/// Archipelago mode: the multiworld decides what every major pickup gives, so in the ROM they all become
/// <see cref="Collectable.AP_ITEM"/>, which gives nothing and only sets the location's collected flag.
/// </summary>
public static class ArchipelagoItems
{
    /// <summary>
    /// Items, spells, sword techs, heart and magic containers and quest items. Not P-bags, jars, small keys or 1-ups.
    /// </summary>
    public static bool IsMajorItem(this Collectable collectable)
    {
        return collectable != Collectable.AP_ITEM && !collectable.IsMinorItem() && !collectable.IsInternalUse();
    }

    /// <summary>
    /// Replaces every major item at the given locations with the AP item, whether or not it was shuffled.
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
                    if (townMap.Collectable is { } collectable && collectable.IsMajorItem())
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
                    if (room.Collectable is { } collectable && collectable.IsMajorItem())
                    {
                        room.Collectable = Collectable.AP_ITEM;
                        replaced++;
                    }
                }
            }
            else
            {
                var collectables = location.GetAllCollectables();
                if (collectables.Any(c => c.IsMajorItem()))
                {
                    replaced += collectables.Count(c => c.IsMajorItem());
                    location.SetCollectables(collectables.Select(c => c.IsMajorItem() ? Collectable.AP_ITEM : c));
                }
            }
        }
        return replaced;
    }
}
