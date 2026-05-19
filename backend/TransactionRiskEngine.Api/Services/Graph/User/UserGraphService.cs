using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Graph;

public sealed partial class UserGraphService : IUserGraphService {
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly GraphRiskOptions _settings;

    public UserGraphService(
        AppDbContext db,
        IMemoryCache cache,
        IOptions<GraphRiskOptions> options
    ) {
        _db = db;
        _cache = cache;
        _settings = options.Value;
    }

    public async Task<IReadOnlyList<GraphPath>> FindRiskPathsAsync(
        Guid userId,
        int maxDepth,
        CancellationToken cancellationToken
    ) {
        var depth = Math.Clamp(maxDepth, 1, ResolveMaxDepth(_settings));
        var graph = await LoadBoundedGraphAsync(userId, depth, cancellationToken);

        return GraphTraversal.FindRiskPaths(
            graph,
            GraphNodeIds.User(userId),
            depth,
            ResolveMaxPaths(_settings)
        );
    }

    public async Task<GraphResponseDto> BuildGraphAsync(
        Guid userId,
        CancellationToken cancellationToken
    ) {
        var maxDepth = ResolveMaxDepth(_settings);
        var graph = await LoadBoundedGraphAsync(userId, maxDepth, cancellationToken);
        var start = GraphNodeIds.User(userId);
        var visible = GraphTraversal.CollectVisibleNodes(graph, start, maxDepth);

        var nodes = visible
            .Where(graph.Nodes.ContainsKey)
            .Select(id => graph.Nodes[id])
            .Select(n => new GraphNodeDto(n.Id, n.Label, n.Type, n.IsRisky))
            .OrderBy(n => n.Type)
            .ThenBy(n => n.Label)
            .ToList();

        var edges = GraphTraversal
            .BuildUndirectedEdges(graph, visible)
            .Select(edge => new GraphEdgeDto(
                $"{edge.Source}->{edge.Target}",
                edge.Source,
                edge.Target,
                "linked")
            )
            .ToList();

        var riskPaths = GraphTraversal.FindRiskPaths(
            graph,
            start,
            maxDepth,
            ResolveMaxPaths(_settings));

        return new GraphResponseDto(
            userId,
            nodes,
            edges,
            riskPaths.Select(path => string.Join(" -> ", path.Nodes)).ToList());
    }

    private async Task<GraphSnapshot> LoadBoundedGraphAsync(
        Guid userId,
        int maxDepth,
        CancellationToken cancellationToken
    ) {
        var context = CreateBuildContext(_db, _settings);
        var cacheKey = $"graph:{userId:N}:depth:{maxDepth}:edges:{context.MaxEdgesPerExpansion}:nodes:{context.MaxTotalNodes}";
        var graph = await _cache.GetOrCreateAsync(cacheKey, async entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Clamp(_settings.CacheSeconds, 1, 120));
            return await BuildBoundedGraphAsync(context, userId, maxDepth, CancellationToken.None);
        });

        return graph ?? await BuildBoundedGraphAsync(context, userId, maxDepth, cancellationToken);
    }

    private static async Task<GraphSnapshot> BuildBoundedGraphAsync(
        GraphBuildContext context,
        Guid userId,
        int maxDepth,
        CancellationToken cancellationToken
    ) {
        var nodes = new Dictionary<string, GraphNodeInfo>();
        var edges = new Dictionary<string, HashSet<string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { GraphNodeIds.User(userId) };
        var frontier = new HashSet<string>(StringComparer.Ordinal) { GraphNodeIds.User(userId) };

        for (var depth = 0; depth <= maxDepth && frontier.Count > 0; depth++) {
            await LoadNodeMetadataAsync(context, frontier, nodes, cancellationToken);

            if (depth == maxDepth) {
                break;
            }

            var neighbours = await LoadEdgesForFrontierAsync(context, frontier, edges, cancellationToken);
            neighbours.ExceptWith(seen);

            var remainingNodeBudget = Math.Max(0, context.MaxTotalNodes - seen.Count);
            if (neighbours.Count > remainingNodeBudget) {
                neighbours = neighbours
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .Take(remainingNodeBudget)
                    .ToHashSet(StringComparer.Ordinal);
            }

            seen.UnionWith(neighbours);
            frontier = neighbours;
        }

        return new GraphSnapshot(
            nodes,
            edges.ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)x.Value.OrderBy(id => id, StringComparer.Ordinal).ToList()));
    }

    private static GraphBuildContext CreateBuildContext(AppDbContext db, GraphRiskOptions settings) {
        return new GraphBuildContext(
            db,
            Math.Clamp(settings.MaxEdgesPerExpansion, 1, 5000),
            Math.Clamp(settings.MaxTotalNodes, 100, 10000)
        );
    }

    private static int ResolveMaxDepth(GraphRiskOptions settings) => Math.Clamp(settings.MaxDepth, 1, 5);
    private static int ResolveMaxPaths(GraphRiskOptions settings) => Math.Clamp(settings.MaxPaths, 1, 10);

    private sealed record GraphBuildContext(
        AppDbContext Db,
        int MaxEdgesPerExpansion,
        int MaxTotalNodes);

    private static HashSet<Guid> ParseNodeIds(IReadOnlySet<string> nodeIds, string prefix) {
        return nodeIds
            .Where(x => x.StartsWith(prefix, StringComparison.Ordinal))
            .Select(x => x[prefix.Length..])
            .Where(x => Guid.TryParse(x, out _))
            .Select(Guid.Parse)
            .ToHashSet();
    }

    private static void AddEdge(Dictionary<string, HashSet<string>> edges, string left, string right) {
        if (!edges.TryGetValue(left, out var leftEdges)) {
            leftEdges = new HashSet<string>(StringComparer.Ordinal);
            edges[left] = leftEdges;
        }

        if (!edges.TryGetValue(right, out var rightEdges)) {
            rightEdges = new HashSet<string>(StringComparer.Ordinal);
            edges[right] = rightEdges;
        }

        leftEdges.Add(right);
        rightEdges.Add(left);
    }

}
