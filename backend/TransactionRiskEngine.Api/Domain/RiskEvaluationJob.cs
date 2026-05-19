namespace TransactionRiskEngine.Api.Domain;

public sealed class RiskEvaluationJob {
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RequestedBatchSize { get; set; }
    public int ProcessedCount { get; set; }
    public int ChangedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
