namespace TransactionRiskEngine.Api.Dtos;

public sealed record GraphNodeDto(
    string Id,
    string Label,
    string Type,
    bool IsRisky
);

public sealed record GraphEdgeDto(
    string Id,
    string Source,
    string Target,
    string Label
);

public sealed record GraphResponseDto(
    Guid UserId,
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges,
    IReadOnlyList<string> RiskPaths
);
