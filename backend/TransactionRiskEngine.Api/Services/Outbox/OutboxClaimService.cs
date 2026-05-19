using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class OutboxClaimService(IOptions<OutboxDispatcherOptions> options) : IOutboxClaimService {
    public async Task<int> RequeueStaleProcessingMessagesAsync(
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        var now = DateTimeOffset.UtcNow;
        var staleBefore = now.AddSeconds(-Math.Max(30, options.Value.LockTimeoutSeconds));

        var staleMessages = (await db.OutboxMessages
                .Where(x => x.Status == OutboxMessageStatus.Processing)
                .ToListAsync(cancellationToken)
            )
            .Where(x => x.LockedAt < staleBefore)
            .ToList();

        foreach (var message in staleMessages) {
            message.Status = OutboxMessageStatus.Pending;
            message.LockedAt = null;
            message.LockedBy = null;
            message.NextAttemptAt = now.AddSeconds(OutboxDeliveryService.CalculateBackoffSeconds(message.AttemptCount));
        }

        await db.SaveChangesAsync(cancellationToken);
        return staleMessages.Count;
    }

    public async Task<List<OutboxMessage>> ClaimBatchAsync(
        AppDbContext db,
        string workerId,
        CancellationToken cancellationToken
    ) {
        var now = DateTimeOffset.UtcNow;
        var batchSize = Math.Clamp(options.Value.BatchSize, 1, 100);
        var maxAttempts = Math.Max(1, options.Value.MaxAttempts);

        if (!CanUsePostgresSetBasedClaims(db)) {
            return await ClaimBatchWithTrackedEntitiesAsync(db, workerId, now, batchSize, maxAttempts, cancellationToken);
        }

        var candidateIds = await db.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Status == OutboxMessageStatus.Pending &&
                x.AttemptCount < maxAttempts &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now)
            )
            .OrderBy(x => x.OccurredAt)
            .Select(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0) {
            return [];
        }

        await db.OutboxMessages
            .Where(x => candidateIds.Contains(x.Id) && x.Status == OutboxMessageStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, OutboxMessageStatus.Processing)
                .SetProperty(x => x.LockedAt, now)
                .SetProperty(x => x.LockedBy, workerId),
                cancellationToken
            );

        return await db.OutboxMessages
            .Where(x => candidateIds.Contains(x.Id) &&
                x.Status == OutboxMessageStatus.Processing &&
                x.LockedBy == workerId
            )
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<OutboxMessage>> ClaimBatchWithTrackedEntitiesAsync(
        AppDbContext db,
        string workerId,
        DateTimeOffset now,
        int batchSize,
        int maxAttempts,
        CancellationToken cancellationToken
    ) {
        var messages = (await db.OutboxMessages
                .Where(x => x.Status == OutboxMessageStatus.Pending)
                .ToListAsync(cancellationToken)
            )
            .Where(x => x.AttemptCount < maxAttempts)
            .Where(x => x.NextAttemptAt is null || x.NextAttemptAt <= now)
            .OrderBy(x => x.OccurredAt)
            .Take(batchSize)
            .ToList();

        foreach (var message in messages) {
            message.Status = OutboxMessageStatus.Processing;
            message.LockedAt = now;
            message.LockedBy = workerId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return messages;
    }

    private static bool CanUsePostgresSetBasedClaims(AppDbContext db) =>
        db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}
