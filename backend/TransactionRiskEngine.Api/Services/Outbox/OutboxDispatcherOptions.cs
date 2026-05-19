namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class OutboxDispatcherOptions {
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 25;
    public int MaxAttempts { get; set; } = 5;
    public int LockTimeoutSeconds { get; set; } = 120;
}
