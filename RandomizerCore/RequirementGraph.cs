using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Z2Randomizer.RandomizerCore.Overworld;
using Z2Randomizer.RandomizerCore.Sidescroll.Palace;
using Z2Randomizer.RandomizerCore.Sidescroll.Town;

namespace Z2Randomizer.RandomizerCore;

public enum GraphNodeKind
{
    Location,
    Town,
    TownMap,
    Palace,
    PalaceRoom,
}

public enum GraphEdgeKind
{
    /// <summary>Walking between two overworld locations.</summary>
    Walk,
    /// <summary>A cave or town connection between two locations on the same continent.</summary>
    Connection,
    /// <summary>A raft, bridge or cave connecting two continents.</summary>
    ContinentConnection,
    /// <summary>From a town location into the town map where you enter it, or between maps of a town.</summary>
    Town,
    /// <summary>From a palace location to its entrance room, or between rooms of a palace.</summary>
    Palace,
}

/// <param name="HasItem">An item can be picked up here.</param>
/// <param name="IsBoss">This is the boss room of a palace.</param>
public record GraphNode(
    string Id,
    GraphNodeKind Kind,
    string Name,
    string? Continent,
    int? X,
    int? Y,
    bool IsStart,
    bool HasItem,
    bool IsBoss);

/// <param name="Requirements">
/// Alternatives (OR) of requirement sets (AND). An empty array means the edge has no requirements.
/// Implicit requirements (such as JUMP needing TWO_CONTAINERS) are already included.
/// </param>
/// <param name="RequiredItems">Items that must additionally be held which are not RequirementTypes (the raft).</param>
public record GraphEdge(
    string From,
    string To,
    GraphEdgeKind Kind,
    string[][] Requirements,
    string[]? RequiredItems);

public record RequirementGraph(
    string? Seed,
    string? Flags,
    bool MarioMode,
    string Description,
    string StartNode,
    List<GraphNode> Nodes,
    List<GraphEdge> Edges);

[JsonSourceGenerationOptions(
    UseStringEnumConverter = true,
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(RequirementGraph))]
public partial class RequirementGraphSerializationContext : JsonSerializerContext { }

/// <summary>
/// Exports the logic of a generated seed as a graph, starting from the North Palace. Nodes are overworld
/// locations, town maps and palace rooms; an edge can be traversed when the requirements on it are held.
/// The graph describes the logic only and does not say which item is placed where.
/// </summary>
public static class RequirementGraphBuilder
{
    private const string DESCRIPTION =
        "Logic graph starting at startNode. An edge can be traversed if all of the requirements in any one of its "
        + "requirement sets are held (an empty list means no requirements) and all requiredItems are held. "
        + "Nodes with hasItem hold an item, nodes with isBoss are palace bosses. Item identities are not included.";

    private static readonly RequirementType[] TERRAIN_GATES = [RequirementType.BOOTS, RequirementType.HAMMER, RequirementType.FLUTE];

    // OR of ANDs. An empty list is unsatisfiable, a single empty set is "no requirements".
    private static List<HashSet<RequirementType>> And(List<HashSet<RequirementType>> a, List<HashSet<RequirementType>> b)
    {
        List<HashSet<RequirementType>> result = [];
        foreach (var x in a)
        {
            foreach (var y in b)
            {
                HashSet<RequirementType> union = [.. x];
                union.UnionWith(y);
                result.Add(union);
            }
        }
        return Minimize(result);
    }

    private static List<HashSet<RequirementType>> Minimize(List<HashSet<RequirementType>> clauses)
    {
        List<HashSet<RequirementType>> result = [];
        foreach (var clause in clauses.OrderBy(c => c.Count))
        {
            if (!result.Any(kept => kept.IsSubsetOf(clause)))
            {
                result.Add(clause);
            }
        }
        return result;
    }

    private static string[][] ToJagged(List<HashSet<RequirementType>> clauses)
    {
        if (clauses.Any(c => c.Count == 0))
        {
            return [];
        }
        return clauses.Select(c => c.Select(r => r.ToString()).Order().ToArray()).ToArray();
    }

    private sealed class Builder(IReadOnlyList<World> worlds, RandomizerProperties props)
    {
        private readonly List<GraphNode> nodes = [];
        private readonly List<GraphEdge> edges = [];
        private readonly Dictionary<Location, string> locationIds = [];
        private readonly HashSet<string> usedIds = [];

        public RequirementGraph Build()
        {
            var west = worlds.OfType<WestHyrule>().First();
            foreach (var world in worlds)
            {
                foreach (var location in world.AllLocations.Where(IsPlaced))
                {
                    GetNodeId(location, world == west && location == west.northPalace);
                }
            }
            foreach (var world in worlds)
            {
                AddWalkEdges(world);
                AddConnectionEdges(world);
                foreach (var location in world.AllLocations.Where(IsPlaced))
                {
                    AddInteriorEdges(location);
                }
            }
            AddContinentConnectionEdges();

            return new RequirementGraph(
                props.Seed, props.Flags, props.MarioMode, DESCRIPTION,
                GetNodeId(west.northPalace), nodes, edges);
        }

        private static bool IsPlaced(Location location) => location.Y > 0 && location.Xpos >= 0;

        private string GetNodeId(Location location, bool isStart = false)
        {
            if (locationIds.TryGetValue(location, out var existing))
            {
                return existing;
            }
            string id = UniqueId(location.ID.ToString());
            locationIds[location] = id;
            var kind = location.Town != null ? GraphNodeKind.Town
                : location.Palace != null ? GraphNodeKind.Palace
                : GraphNodeKind.Location;
            // towns and palaces keep their items in their maps/rooms
            bool hasItem = kind == GraphNodeKind.Location && location.GetCollectableCount() > 0;
            nodes.Add(new GraphNode(id, kind, string.IsNullOrEmpty(location.Name) ? id : location.Name,
                location.Continent.ToString(), location.Xpos, location.Y, isStart, hasItem, false));
            return id;
        }

        private string UniqueId(string id)
        {
            string unique = id;
            for (int i = 2; !usedIds.Add(unique); i++)
            {
                unique = $"{id}_{i}";
            }
            return unique;
        }

        private void AddEdge(string from, string to, GraphEdgeKind kind, List<HashSet<RequirementType>> clauses, string[]? requiredItems = null)
        {
            if (clauses.Count == 0)
            {
                return;
            }
            edges.Add(new GraphEdge(from, to, kind, ToJagged(clauses), requiredItems));
        }

        /// <summary>
        /// Edges between each location and the locations that can be walked to from it without passing through another
        /// location, labelled with the terrain items (boots, hammer, flute) needed to cross the terrain in between.
        /// </summary>
        private void AddWalkEdges(World world)
        {
            var placed = world.AllLocations
                .Where(l => IsPlaced(l) && l.Y < world.MapRows && l.Xpos < world.MapColumns)
                .ToList();
            var byTile = placed.GroupBy(l => (l.Y, l.Xpos)).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var from in placed)
            {
                Dictionary<Location, List<HashSet<RequirementType>>> found = [];
                for (int mask = 0; mask < 1 << TERRAIN_GATES.Length; mask++)
                {
                    HashSet<RequirementType> allowed = [.. TERRAIN_GATES.Where((_, i) => (mask & (1 << i)) != 0)];
                    bool[,] covered = new bool[world.MapRows, world.MapColumns];
                    covered[from.Y, from.Xpos] = true;
                    Stack<(int y, int x)> toVisit = new();
                    toVisit.Push((from.Y, from.Xpos));
                    while (toVisit.Count > 0)
                    {
                        var (y, x) = toVisit.Pop();
                        foreach (var (ny, nx) in new[] { (y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1) })
                        {
                            if (ny < 0 || nx < 0 || ny >= world.MapRows || nx >= world.MapColumns || covered[ny, nx])
                            {
                                continue;
                            }
                            covered[ny, nx] = true;
                            if (!world.TryGetWalkRequirement(world.map[ny, nx], out var gate)
                                || (gate != null && !allowed.Contains(gate.Value)))
                            {
                                continue;
                            }
                            if (byTile.TryGetValue((ny, nx), out var others))
                            {
                                foreach (var other in others.Where(o => o != from))
                                {
                                    if (!found.TryGetValue(other, out var list))
                                    {
                                        found[other] = list = [];
                                    }
                                    list.Add(allowed);
                                }
                                continue;
                            }
                            toVisit.Push((ny, nx));
                        }
                    }
                }
                foreach (var (to, gates) in found)
                {
                    AddEdge(GetNodeId(from), GetNodeId(to), GraphEdgeKind.Walk,
                        And(Minimize(gates), to.AccessRequirements.ToClauses()));
                }
            }
        }

        private void AddConnectionEdges(World world)
        {
            IEnumerable<(Location from, Location to)> pairs = world.connections.Select(kv => (kv.Key, kv.Value));
            if (world is DeathMountain dm)
            {
                pairs = pairs.Concat(dm.connectionsDM.SelectMany(kv => kv.Value.Select(to => (kv.Key, to))));
            }
            foreach (var (from, to) in pairs.Where(p => IsPlaced(p.from)))
            {
                var traversal = from.Town != null ? TownTraversal(from.Town) : new List<HashSet<RequirementType>> { new() };
                AddEdge(GetNodeId(from), GetNodeId(to), GraphEdgeKind.Connection, traversal);
            }
        }

        /// <summary>Requirements to walk through a town from its west entrance to its outside exit. Mirrors Town.CanBeTraversed.</summary>
        private static List<HashSet<RequirementType>> TownTraversal(Town town)
        {
            List<HashSet<RequirementType>> result = [[]];
            var map = town.TownMaps.FirstOrDefault(m => m.Left == null && !m.IsInternalLocation);
            HashSet<TownMap> seen = [];
            while (map != null && seen.Add(map))
            {
                result = And(result, map.AccessRequirements.ToClauses());
                if (map.RightExitIsOutside)
                {
                    return result;
                }
                map = map.Right;
            }
            return [];
        }

        private void AddContinentConnectionEdges()
        {
            var connectors = new (string? item, Func<World, Location?> get)[]
            {
                (Collectable.RAFT.ToString(), w => w.raft),
                (null, w => w.bridge),
                (null, w => w.cave1),
                (null, w => w.cave2),
            };
            foreach (var (item, get) in connectors)
            {
                var present = worlds.Select(get).Where(l => l != null && IsPlaced(l)).Select(l => l!).ToList();
                foreach (var from in present)
                {
                    foreach (var to in present.Where(l => l != from))
                    {
                        AddEdge(GetNodeId(from), GetNodeId(to), GraphEdgeKind.ContinentConnection,
                            to.AccessRequirements.ToClauses(), item == null ? null : [item]);
                    }
                }
            }
        }

        private void AddInteriorEdges(Location location)
        {
            if (location.Town != null)
            {
                AddTownEdges(location, location.Town);
            }
            else if (location.Palace != null)
            {
                AddPalaceEdges(location, location.Palace);
            }
        }

        /// <summary>Mirrors Town.GetGettableItems: a map's requirements are needed to use it and to move on from it.</summary>
        private void AddTownEdges(Location location, Town town)
        {
            if (town.TownMaps.Count == 0)
            {
                return;
            }
            bool fromEast = location.ForceEnterRight;
            var start = fromEast
                ? town.TownMaps.FirstOrDefault(m => m.Right == null)
                : town.TownMaps.FirstOrDefault(m => m.Left == null);
            if (start == null)
            {
                return;
            }
            string locationId = GetNodeId(location);
            Dictionary<TownMap, string> ids = [];
            string MapId(TownMap map)
            {
                if (!ids.TryGetValue(map, out var id))
                {
                    id = UniqueId($"{locationId}:map{map.Map}");
                    ids[map] = id;
                    nodes.Add(new GraphNode(id, GraphNodeKind.TownMap,
                        string.IsNullOrEmpty(map.Name) ? $"{town.Name} map {map.Map}" : $"{town.Name} - {map.Name}",
                        location.Continent.ToString(), null, null, false, map.Collectable != null, false));
                }
                return id;
            }

            AddEdge(locationId, MapId(start), GraphEdgeKind.Town, start.AccessRequirements.ToClauses());
            Queue<TownMap> pending = new([start]);
            HashSet<TownMap> visited = [start];
            while (pending.Count > 0)
            {
                var map = pending.Dequeue();
                List<TownMap?> next = [map.Door0, map.Door1, map.Door2, map.Door3];
                if (!fromEast && (!map.IsInternalLocation || (map.Map == 57 && town.Type == TownType.DARUNIA)))
                {
                    next.Add(map.Right);
                }
                if (fromEast && !map.IsInternalLocation)
                {
                    next.Add(map.Left);
                }
                foreach (var target in next.OfType<TownMap>())
                {
                    AddEdge(MapId(map), MapId(target), GraphEdgeKind.Town, target.AccessRequirements.ToClauses());
                    if (visited.Add(target))
                    {
                        pending.Enqueue(target);
                    }
                }
            }
        }

        /// <summary>Mirrors Palace.GetGettableItems: moving into a room needs that room's requirements.</summary>
        private void AddPalaceEdges(Location location, Palace palace)
        {
            var entrance = palace.AllRooms.FirstOrDefault(r => r.IsEntrance);
            if (entrance == null)
            {
                return;
            }
            string locationId = GetNodeId(location);
            Dictionary<Room, string> ids = [];
            for (int i = 0; i < palace.AllRooms.Count; i++)
            {
                var room = palace.AllRooms[i];
                string id = UniqueId($"{locationId}:room{i}");
                ids[room] = id;
                nodes.Add(new GraphNode(id, GraphNodeKind.PalaceRoom,
                    string.IsNullOrEmpty(room.Name) ? $"Palace {palace.Number} room {i}" : room.Name,
                    location.Continent.ToString(), room.coords.X, room.coords.Y, false,
                    palace.ItemRooms.Contains(room), room == palace.BossRoom));
            }

            AddEdge(locationId, ids[entrance], GraphEdgeKind.Palace, [[]]);
            foreach (var room in palace.AllRooms)
            {
                foreach (var neighbor in new[] { room.Left, room.Right, room.Up, room.Down }.OfType<Room>().Distinct())
                {
                    if (ids.TryGetValue(neighbor, out var neighborId))
                    {
                        AddEdge(ids[room], neighborId, GraphEdgeKind.Palace,
                            neighbor.GetEffectiveRequirements(props.MarioMode).ToClauses());
                    }
                }
            }
        }
    }

    public static RequirementGraph Build(IReadOnlyList<World> worlds, RandomizerProperties props)
    {
        return new Builder(worlds, props).Build();
    }
}
