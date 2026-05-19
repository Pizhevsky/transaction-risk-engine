using TransactionRiskEngine.Api.Services.Graph;

namespace TransactionRiskEngine.Tests;

public sealed class GraphTraversalTests {
    [Fact]
    public void Finds_two_hop_risk_path() {
        var graph = Graph(
            Nodes(
                User("U:1", "Alex"),
                Device("D:1", "Shared device"),
                User("U:2", "Flagged user", risky: true)
            ),
            Edges(
                ("U:1", "D:1"),
                ("D:1", "U:2")
            )
        );

        var paths = GraphTraversal.FindRiskPaths(graph, "U:1", maxDepth: 3, maxPaths: 3);

        var path = Assert.Single(paths);
        Assert.Equal(new[] { "Alex", "Shared device", "Flagged user" }, path.Nodes);
        Assert.Equal("Flagged user", path.RiskLabel);
        Assert.True(path.IsRisky);
    }

    [Fact]
    public void Respects_depth_limit() {
        var graph = Graph(
            Nodes(
                User("U:1", "Alex"),
                Device("D:1", "Device"),
                Card("C:1", "Card"),
                User("U:2", "Too far", risky: true)
            ),
            Edges(
                ("U:1", "D:1"),
                ("D:1", "C:1"),
                ("C:1", "U:2")
            )
        );

        var paths = GraphTraversal.FindRiskPaths(graph, "U:1", maxDepth: 2, maxPaths: 3);

        Assert.Empty(paths);
    }

    [Fact]
    public void Handles_cycles_without_revisiting_forever() {
        var graph = Graph(
            Nodes(
                User("U:1", "Alex"),
                Device("D:1", "Device"),
                Card("C:1", "Card"),
                User("U:2", "Flagged", risky: true)
            ),
            Edges(
                ("U:1", "D:1"),
                ("D:1", "C:1"),
                ("C:1", "U:1"),
                ("C:1", "U:2")
            )
        );

        var paths = GraphTraversal.FindRiskPaths(graph, "U:1", maxDepth: 3, maxPaths: 3);

        var path = Assert.Single(paths);
        Assert.Equal("Flagged", path.RiskLabel);
    }

    [Fact]
    public void Stops_after_configured_number_of_paths() {
        var graph = Graph(
            Nodes(
                User("U:1", "Alex"),
                Device("D:1", "Device one", risky: true),
                Device("D:2", "Device two", risky: true),
                Device("D:3", "Device three", risky: true),
                Device("D:4", "Device four", risky: true)
            ),
            Edges(
                ("U:1", "D:1"),
                ("U:1", "D:2"),
                ("U:1", "D:3"),
                ("U:1", "D:4")
            )
        );

        var paths = GraphTraversal.FindRiskPaths(graph, "U:1", maxDepth: 3, maxPaths: 3);

        Assert.Equal(3, paths.Count);
    }

    private static GraphSnapshot Graph(
        IReadOnlyDictionary<string, GraphNodeInfo> nodes,
        params (string Left, string Right)[] edges
    ) {
        var map = nodes.Keys.ToDictionary(id => id, _ => new List<string>());
        foreach (var (left, right) in edges) {
            map[left].Add(right);
            map[right].Add(left);
        }

        return new GraphSnapshot(
            nodes,
            map.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value)
        );
    }

    private static IReadOnlyDictionary<string, GraphNodeInfo> Nodes(params GraphNodeInfo[] nodes) =>
        nodes.ToDictionary(x => x.Id);

    private static GraphNodeInfo User(string id, string label, bool risky = false) =>
        new(id, label, "User", risky);

    private static GraphNodeInfo Device(string id, string label, bool risky = false) =>
        new(id, label, "Device", risky);

    private static GraphNodeInfo Card(string id, string label, bool risky = false) =>
        new(id, label, "Card", risky);

    private static (string Left, string Right)[] Edges(params (string Left, string Right)[] edges) => edges;
}
