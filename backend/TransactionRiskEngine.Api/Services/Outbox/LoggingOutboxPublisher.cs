using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class LoggingOutboxPublisher(ILogger<LoggingOutboxPublisher> logger) : IOutboxPublisher {
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) {
        logger.LogInformation(
            "Outbox message published: {OutboxMessageId} {OutboxMessageType} {Payload}",
            message.Id,
            message.Type,
            message.PayloadJson
        );

        return Task.CompletedTask;
    }
}
