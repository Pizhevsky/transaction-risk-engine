namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class OutboxPublisherOptions {
    public string? HttpEndpoint { get; set; }
    public bool LogPublishedMessages { get; set; } = true;
}
