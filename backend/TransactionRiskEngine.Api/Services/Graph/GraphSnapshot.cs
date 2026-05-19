namespace TransactionRiskEngine.Api.Services.Graph;

public sealed record GraphSnapshot(
    IReadOnlyDictionary<string, GraphNodeInfo> Nodes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Edges
);
