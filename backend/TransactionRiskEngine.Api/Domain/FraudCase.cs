namespace TransactionRiskEngine.Api.Domain;

public sealed class FraudCase {
    public Guid Id { get; set; }
    public Guid TransactionRecordId { get; set; }
    public TransactionRecord TransactionRecord { get; set; } = default!;

    public FraudCaseStatus Status { get; set; } = FraudCaseStatus.Open;
    public string Summary { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
}
