using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Outbox;

namespace TransactionRiskEngine.Api.Services.Risk;

public sealed partial class RiskEvaluationService {
    private async Task<Guid> SaveJobAsync(
        int requestedBatchSize,
        int processedCount,
        int changedCount,
        string reason,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken
    ) {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(db, cancellationToken);
        var completedAt = DateTimeOffset.UtcNow;

        var job = new RiskEvaluationJob {
            Id = Guid.NewGuid(),
            Reason = reason,
            RequestedBatchSize = requestedBatchSize,
            ProcessedCount = processedCount,
            ChangedCount = changedCount,
            CreatedAt = startedAt,
            CompletedAt = completedAt
        };

        db.RiskEvaluationJobs.Add(job);
        new OutboxWriter(db).Add(
            "risk-rules.evaluated",
            new {
                jobId = job.Id,
                processedCount,
                changedCount,
                reason
            },
            completedAt
        );

        await db.SaveChangesAsync(cancellationToken);
        await CommitIfStartedAsync(transaction, cancellationToken);
        return job.Id;
    }

    private static async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        return db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private static async Task CommitIfStartedAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken
    ) {
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
