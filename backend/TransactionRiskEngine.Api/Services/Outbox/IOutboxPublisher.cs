using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public interface IOutboxPublisher {
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
