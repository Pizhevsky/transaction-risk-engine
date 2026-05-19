using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Graph;

internal sealed class TransactionGraphBuilder(Guid userId, string userName) {
    private readonly Dictionary<string, GraphNodeDto> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphEdgeDto> _edges = new(StringComparer.Ordinal);
    private readonly List<string> _riskPaths = [];
    private readonly string _userNode = GraphNodeIds.User(userId);

    public void AddEntityNode(string nodeId, string label, string type, bool isRisky) {
        AddNode(nodeId, label, type, isRisky);
        AddEdge(_userNode, nodeId);

        if (isRisky) {
            _riskPaths.Add($"{userName} -> {label}");
        }
    }

    public void AddRelatedUsers(
        string entityNode,
        string entityLabel,
        IReadOnlyCollection<RelatedGraphUser> users
    ) {
        foreach (var user in users) {
            var userNode = GraphNodeIds.User(user.Id);
            AddNode(userNode, user.DisplayName, "User", user.IsRisky);
            AddEdge(entityNode, userNode);

            if (user.IsRisky) {
                _riskPaths.Add($"{userName} -> {entityLabel} -> {user.DisplayName}");
            }
        }
    }

    public void AddNode(string id, string label, string type, bool isRisky) {
        _nodes[id] = new GraphNodeDto(id, label, type, isRisky);
    }

    public GraphResponseDto ToResponse() {
        return new GraphResponseDto(
            userId,
            _nodes.Values
                .OrderByDescending(x => x.Id == _userNode)
                .ThenBy(x => x.Type)
                .ThenBy(x => x.Label)
                .ToList(),
            _edges.Values
                .OrderBy(x => x.Source)
                .ThenBy(x => x.Target)
                .ToList(),
            _riskPaths.Distinct(StringComparer.Ordinal).Take(3).ToList()
        );
    }

    private void AddEdge(string source, string target) {
        var ordered = string.CompareOrdinal(source, target) <= 0
            ? (Source: source, Target: target)
            : (Source: target, Target: source);
        var edgeId = $"{ordered.Source}->{ordered.Target}";

        _edges[edgeId] = new GraphEdgeDto(edgeId, ordered.Source, ordered.Target, "linked");
    }
}

internal sealed record RelatedGraphUser(
    Guid Id,
    string DisplayName,
    bool IsRisky);
