namespace TransactionRiskEngine.Api.Domain;

public sealed class AuditLog {
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? EntityCode { get; set; }
    public Guid? TransactionRecordId { get; set; }
    public TransactionRecord? TransactionRecord { get; set; }
    public string? CorrelationId { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
