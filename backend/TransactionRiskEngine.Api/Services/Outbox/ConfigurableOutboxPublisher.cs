using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class ConfigurableOutboxPublisher(
    LoggingOutboxPublisher loggingPublisher,
    HttpOutboxPublisher httpPublisher,
    IOptions<OutboxPublisherOptions> options
) : IOutboxPublisher {
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) {
        if (!string.IsNullOrWhiteSpace(options.Value.HttpEndpoint)) {
            await httpPublisher.PublishAsync(message, cancellationToken);
        }

        if (options.Value.LogPublishedMessages) {
            await loggingPublisher.PublishAsync(message, cancellationToken);
        }
    }
}
