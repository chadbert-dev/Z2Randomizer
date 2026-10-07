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
}
