namespace TransactionRiskEngine.Api.Services.Outbox;

public interface IOutboxWriter {
    void Add<TPayload>(string type, TPayload payload, DateTimeOffset occurredAt);
}
