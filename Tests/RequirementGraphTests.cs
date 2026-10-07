using System.Text.Json;
using FluentAssertions;
using Z2Randomizer.RandomizerCore;

namespace Z2Randomizer.Tests;

[TestClass]
public class RequirementGraphTests
{
    [TestMethod]
    public void ToClauses_EmptyRequirements_IsSingleEmptyClause()
    {
        var clauses = Requirements.NONE.ToClauses();

        clauses.Should().HaveCount(1);
        clauses[0].Should().BeEmpty();
    }

    [TestMethod]
    public void ToClauses_AddsImplicitRequirements()
    {
        var clauses = new Requirements([RequirementType.JUMP], [[RequirementType.FAIRY, RequirementType.KEY]]).ToClauses();

        clauses.Should().HaveCount(2);
        clauses[0].Should().BeEquivalentTo([RequirementType.JUMP, RequirementType.TWO_CONTAINERS]);
        clauses[1].Should().BeEquivalentTo([RequirementType.FAIRY, RequirementType.KEY, RequirementType.FOUR_CONTAINERS]);
    }

    [TestMethod]
    public void ToClauses_MatchesAreSatisfiedBy()
    {
        var requirements = new Requirements([RequirementType.JUMP], [[RequirementType.HAMMER, RequirementType.FAIRY]]);
        var clauses = requirements.ToClauses();
        RequirementType[][] holdings = [
            [],
            [RequirementType.JUMP],
            [RequirementType.JUMP, RequirementType.TWO_CONTAINERS],
            [RequirementType.HAMMER, RequirementType.FAIRY],
            [RequirementType.HAMMER, RequirementType.FAIRY, RequirementType.FOUR_CONTAINERS],
        ];

        foreach (var holding in holdings)
        {
            HashSet<RequirementType> held = [.. holding];
            clauses.Any(c => c.IsSubsetOf(held)).Should().Be(requirements.AreSatisfiedBy(held), string.Join(",", holding));
        }
    }

    [TestMethod]
    public void Serialization_RoundTrips()
    {
        var graph = new RequirementGraph(
            "123", "flags", false, "desc", "a",
            [
                new GraphNode("a", GraphNodeKind.Palace, "A", "WEST", 1, 2, true, false, false),
                new GraphNode("b", GraphNodeKind.PalaceRoom, "B", "WEST", null, null, false, true, true),
            ],
            [new GraphEdge("a", "b", GraphEdgeKind.Palace, [["FAIRY", "KEY"], ["JUMP"]], ["RAFT"])]);

        string json = JsonSerializer.Serialize(graph, RequirementGraphSerializationContext.Default.RequirementGraph);
        var parsed = JsonSerializer.Deserialize(json, RequirementGraphSerializationContext.Default.RequirementGraph)!;

        json.Should().Contain("\"startNode\": \"a\"").And.Contain("\"kind\": \"Palace\"");
        parsed.Nodes.Should().Equal(graph.Nodes);
        parsed.Edges.Should().HaveCount(1);
        parsed.Edges[0].Requirements.Should().BeEquivalentTo(graph.Edges[0].Requirements);
        parsed.Edges[0].RequiredItems.Should().Equal("RAFT");
    }

    private static GraphNode Node(string id, bool hasItem = false, bool isBoss = false) =>
        new(id, GraphNodeKind.Location, id, null, null, null, id == "start", hasItem, isBoss);

    private static GraphEdge Edge(string from, string to, string[][] reqs, string[]? items = null) =>
        new(from, to, GraphEdgeKind.Walk, reqs, items);

    [TestMethod]
    public void BuildRoutes_KeepsMinimalAlternativesAndShortestPaths()
    {
        var graph = new RequirementGraph(null, null, false, "", "start",
            [Node("start"), Node("a"), Node("b"), Node("c"), Node("item", hasItem: true), Node("boss", isBoss: true), Node("unreachable", hasItem: true)],
            [
                Edge("start", "a", []),
                Edge("a", "item", [["HAMMER"]]),
                // longer way to the item that needs strictly more, so it must be dropped
                Edge("start", "b", []),
                Edge("b", "c", [["HAMMER", "JUMP"]]),
                Edge("c", "item", []),
                // alternative that needs different things, so it is kept
                Edge("start", "item", [["FLUTE"], ["BOOTS", "JUMP"]]),
                Edge("a", "boss", [["FAIRY"]], ["RAFT"]),
                Edge("unreachable", "start", []),
            ]);

        var routes = RequirementGraphBuilder.BuildRoutes(graph);

        var item = routes.Targets.Single(t => t.Target == "item").Routes;
        item.Select(r => string.Join("+", r.Requirements)).Should()
            .BeEquivalentTo(["HAMMER", "FLUTE", "BOOTS+JUMP"]);
        item.Single(r => r.Requirements.SequenceEqual(["HAMMER"])).Route.Should().Equal("start", "a", "item");

        var boss = routes.Targets.Single(t => t.Target == "boss").Routes.Single();
        boss.Requirements.Should().BeEquivalentTo(["FAIRY", "RAFT"]);

        routes.Targets.Single(t => t.Target == "unreachable").Routes.Should().BeEmpty();
        routes.Nodes.Select(n => n.Id).Should().NotContain(["b", "c"]);
    }

    [TestMethod]
    public void BuildRoutes_HandlesCycles()
    {
        var graph = new RequirementGraph(null, null, false, "", "start",
            [Node("start"), Node("a"), Node("item", hasItem: true)],
            [
                Edge("start", "a", []),
                Edge("a", "start", []),
                Edge("a", "item", [["JUMP"]]),
            ]);

        var routes = RequirementGraphBuilder.BuildRoutes(graph);

        routes.Targets.Single().Routes.Should().ContainSingle()
            .Which.Route.Should().Equal("start", "a", "item");
    }

    [TestMethod]
    public void BuildRoutes_HandlesIdenticalParallelEdges()
    {
        var graph = new RequirementGraph(null, null, false, "", "start",
            [Node("start"), Node("item", hasItem: true)],
            [Edge("start", "item", [["JUMP"]]), Edge("start", "item", [["JUMP"]])]);

        var routes = RequirementGraphBuilder.BuildRoutes(graph);

        routes.Targets.Single().Routes.Should().ContainSingle();
    }
}
