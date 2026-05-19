namespace TransactionRiskEngine.Api.Services.Graph;

public sealed record GraphNodeInfo(
    string Id,
    string Label,
    string Type,
    bool IsRisky
);
