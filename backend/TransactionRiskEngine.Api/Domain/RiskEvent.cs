namespace TransactionRiskEngine.Api.Domain;

public sealed class RiskEvent {
    public Guid Id { get; set; }
    public Guid TransactionRecordId { get; set; }
    public TransactionRecord TransactionRecord { get; set; } = default!;

    public string Code { get; set; } = string.Empty;

    // Score produced by the detector before rule weighting. This is immutable evidence severity.
    public int BaseScore { get; set; }

    // Score after the current rule policy is applied. Rule evaluation updates this field only.
    public int Score { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
}
