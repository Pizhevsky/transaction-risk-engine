namespace TransactionRiskEngine.Api.Services.Risk;

public sealed record RiskEvaluationResult(
    Guid JobId,
    int ProcessedCount,
    int ChangedCount
);
