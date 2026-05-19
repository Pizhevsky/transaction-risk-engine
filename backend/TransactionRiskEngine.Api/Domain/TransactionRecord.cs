namespace TransactionRiskEngine.Api.Domain;

public sealed class TransactionRecord {
    public Guid Id { get; set; }
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = default!;

    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }

    public Guid? PaymentCardId { get; set; }
    public PaymentCard? PaymentCard { get; set; }

    public Guid? IpAddressRecordId { get; set; }
    public IpAddressRecord? IpAddressRecord { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NZD";
    public string Merchant { get; set; } = string.Empty;
    public bool Successful { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? IdempotencyKey { get; set; }
    public string? RequestHash { get; set; }

    public int RiskScore { get; set; }
    public TransactionDecision Decision { get; set; }

    public List<RiskEvent> RiskEvents { get; set; } = [];
    public List<AuditLog> AuditLogs { get; set; } = [];
    public FraudCase? FraudCase { get; set; }
}
