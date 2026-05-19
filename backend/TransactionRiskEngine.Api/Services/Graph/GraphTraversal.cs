namespace TransactionRiskEngine.Api.Services.Graph;

public static class GraphTraversal {
    public static IReadOnlyList<GraphPath> FindRiskPaths(
        GraphSnapshot graph,
        string start,
        int maxDepth,
        int maxPaths
    ) {
        if (!graph.Nodes.ContainsKey(start)) {
            return [];
        }

        var traversal = CreateTraversal(start);
        while (traversal.Queue.Count > 0) {
            var (node, depth) = traversal.Queue.Dequeue();

            if (AddRiskPathIfNeeded(graph, traversal, node, depth, maxPaths)) {
                break;
            }

            EnqueueNeighboursWithinDepth(graph, traversal, node, depth, maxDepth);
        }

        return traversal.Paths;
    }

    public static IReadOnlySet<string> CollectVisibleNodes(
        GraphSnapshot graph,
        string start,
        int maxDepth
    ) {
        var visible = new HashSet<string>();
        if (!graph.Nodes.ContainsKey(start)) {
            return visible;
        }

        var queue = new Queue<(string Node, int Depth)>();
        queue.Enqueue((start, 0));
        visible.Add(start);

        while (queue.Count > 0) {
            var (node, depth) = queue.Dequeue();
            if (depth >= maxDepth) {
                continue;
            }

            foreach (var next in GetNeighbours(graph, node)) {
                if (visible.Add(next)) {
                    queue.Enqueue((next, depth + 1));
                }
            }
        }

        return visible;
    }

    public static IReadOnlyList<(string Source, string Target)> BuildUndirectedEdges(
        GraphSnapshot graph,
        IReadOnlySet<string> visible
    ) {
        var edges = new List<(string Source, string Target)>();
        var edgeIds = new HashSet<string>();

        foreach (var edge in EnumerateVisibleEdges(graph, visible)) {
            var ordered = OrderEdge(edge.Source, edge.Target);
            if (edgeIds.Add(EdgeKey(ordered))) {
                edges.Add(ordered);
            }
        }

        return edges;
    }

    private static IEnumerable<(string Source, string Target)> EnumerateVisibleEdges(
        GraphSnapshot graph,
        IReadOnlySet<string> visible
    ) {
        foreach (var source in visible) {
            foreach (var target in GetNeighbours(graph, source).Where(visible.Contains)) {
                yield return (source, target);
            }
        }
    }

    private static (string Source, string Target) OrderEdge(string source, string target) {
        return string.CompareOrdinal(source, target) <= 0
            ? (source, target)
            : (target, source);
    }

    private static string EdgeKey((string Source, string Target) edge) => $"{edge.Source}->{edge.Target}";

    private static IEnumerable<string> GetNeighbours(GraphSnapshot graph, string node) {
        return graph.Edges.TryGetValue(node, out var neighbours)
            ? neighbours
            : [];
    }

    private static RiskPathTraversal CreateTraversal(string start) {
        var traversal = new RiskPathTraversal(
            new Queue<(string Node, int Depth)>(),
            new HashSet<string> { start },
            new Dictionary<string, string?> { [start] = null },
            []
        );

        traversal.Queue.Enqueue((start, 0));
        return traversal;
    }

    private static bool AddRiskPathIfNeeded(
        GraphSnapshot graph,
        RiskPathTraversal traversal,
        string node,
        int depth,
        int maxPaths
    ) {
        if (depth == 0 || !graph.Nodes[node].IsRisky) {
            return false;
        }

        traversal.Paths.Add(ToPath(node, traversal.Previous, graph.Nodes));
        return traversal.Paths.Count >= maxPaths;
    }

    private static void EnqueueNeighboursWithinDepth(
        GraphSnapshot graph,
        RiskPathTraversal traversal,
        string node,
        int depth,
        int maxDepth
    ) {
        if (depth >= maxDepth) {
            return;
        }

        foreach (var next in GetNeighbours(graph, node)) {
            EnqueueIfUnvisited(traversal, next, node, depth + 1);
        }
    }

    private static void EnqueueIfUnvisited(
        RiskPathTraversal traversal,
        string next,
        string previous,
        int depth
    ) {
        if (!traversal.Visited.Add(next)) {
            return;
        }

        traversal.Previous[next] = previous;
        traversal.Queue.Enqueue((next, depth));
    }

    private static GraphPath ToPath(
        string target,
        IReadOnlyDictionary<string, string?> previous,
        IReadOnlyDictionary<string, GraphNodeInfo> nodes
    ) {
        var path = new List<string>();
        string? current = target;

        while (current is not null) {
            path.Add(nodes[current].Label);
            current = previous[current];
        }

        path.Reverse();
        return new GraphPath(path, nodes[target].Label, nodes[target].IsRisky);
    }

    private sealed record RiskPathTraversal(
        Queue<(string Node, int Depth)> Queue,
        HashSet<string> Visited,
        Dictionary<string, string?> Previous,
        List<GraphPath> Paths
    );
}
