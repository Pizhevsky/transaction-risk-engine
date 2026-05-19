using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class OutboxDeliveryService(
    IOutboxPublisher publisher,
    IOptions<OutboxDispatcherOptions> options
) : IOutboxDeliveryService {
    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken) {
        try {
            await publisher.PublishAsync(message, cancellationToken);

            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.LockedAt = null;
            message.LockedBy = null;
            message.AttemptCount++;
            message.LastError = null;
            message.NextAttemptAt = null;
        } catch (Exception ex) {
            message.AttemptCount++;
            message.LastError = ex.Message;
            message.LockedAt = null;
            message.LockedBy = null;

            if (message.AttemptCount >= Math.Max(1, options.Value.MaxAttempts)) {
                message.Status = OutboxMessageStatus.Failed;
                message.NextAttemptAt = null;
            } else {
                message.Status = OutboxMessageStatus.Pending;
                message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(CalculateBackoffSeconds(message.AttemptCount));
            }
        }
    }

    internal static int CalculateBackoffSeconds(int attemptCount) {
        return Math.Min(300, (int)Math.Pow(2, Math.Clamp(attemptCount, 1, 8)) * 5);
    }
}
