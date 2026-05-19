using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;

namespace TransactionRiskEngine.Api.Services.Risk;

public sealed partial class RiskEvaluationService(
    IDbContextFactory<AppDbContext> dbFactory,
    IRiskRuleCatalog riskRuleCatalog,
    RiskEvaluationRecordProcessor recordProcessor
) : IRiskEvaluationService {
    private const int MaxRequestedBatchSize = 1000;
    private const int InternalChunkSize = 100;

    public async Task<RiskEvaluationResult> EvaluateRecentTransactionsAsync(
        int batchSize,
        string reason,
        CancellationToken cancellationToken
    ) {
        var safeBatchSize = Math.Clamp(batchSize, 1, MaxRequestedBatchSize);
        var rules = await riskRuleCatalog.GetRulesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var targetIds = await LoadTargetTransactionIdsAsync(safeBatchSize, cancellationToken);
        var processedCount = 0;
        var changedCount = 0;

        foreach (var chunk in targetIds.Chunk(InternalChunkSize)) {
            var chunkResult = await ProcessChunkAsync(
                chunk,
                rules,
                reason,
                now,
                cancellationToken
            );

            processedCount += chunkResult.Processed;
            changedCount += chunkResult.Changed;
        }

        var jobId = await SaveJobAsync(
            safeBatchSize,
            processedCount,
            changedCount,
            reason,
            now,
            cancellationToken
        );

        return new RiskEvaluationResult(jobId, processedCount, changedCount);
    }

    private async Task<IReadOnlyList<Guid>> LoadTargetTransactionIdsAsync(
        int batchSize,
        CancellationToken cancellationToken
    ) {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Transactions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(batchSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<(int Processed, int Changed)> ProcessChunkAsync(
        IReadOnlyCollection<Guid> transactionIds,
        IReadOnlyDictionary<string, RiskRuleSnapshot> rules,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken
    ) {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(db, cancellationToken);

        var records = await db.Transactions
            .Include(x => x.RiskEvents)
            .Include(x => x.FraudCase)
            .Where(x => transactionIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var changedCount = 0;
        foreach (var record in records) {
            if (await recordProcessor.EvaluateAsync(record, rules, reason, now, db, cancellationToken)) {
                changedCount++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await CommitIfStartedAsync(transaction, cancellationToken);
        db.ChangeTracker.Clear();

        return (records.Count, changedCount);
    }

}
