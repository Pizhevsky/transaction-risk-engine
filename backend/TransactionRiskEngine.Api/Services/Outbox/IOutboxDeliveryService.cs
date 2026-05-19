using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public interface IOutboxDeliveryService {
    Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken);
}
