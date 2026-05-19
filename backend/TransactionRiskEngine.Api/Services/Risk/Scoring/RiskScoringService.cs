using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Infrastructure;
using TransactionRiskEngine.Api.Services.Outbox;
using TransactionRiskEngine.Api.Services.Transactions;

namespace TransactionRiskEngine.Api.Services.Risk;

public sealed partial class RiskScoringService(
    AppDbContext db,
    ITransactionEntityResolver entityResolver,
    IRiskSignalBuilder signalBuilder,
    ICorrelationIdAccessor correlationIdAccessor,
    IOutboxWriter outboxWriter
) : IRiskScoringService {
    public async Task<AnalyseTransactionResult> AnalyseAndStoreAsync(
        AnalyseTransactionRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken
    ) {
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey ?? request.IdempotencyKey);
        var requestHash = normalizedKey is null ? null : IdempotencyRequestHasher.Compute(request);
        var existing = await FindExistingByIdempotencyKeyAsync(request.UserId, normalizedKey, cancellationToken);
        if (existing is not null) {
            var conflict = CheckIdempotencyPayload(existing, requestHash);
            if (conflict is not null) {
                return AnalyseTransactionResult.Conflict(conflict);
            }

            return AnalyseTransactionResult.Replay(AnalyseTransactionResponseMapper.FromRecord(existing, existing.UserProfile, existing.RiskEvents));
        }

        for (var attempt = 0; attempt < 2; attempt++) {
            try {
                return await CreateNewAnalysisAsync(request, normalizedKey, requestHash, cancellationToken);
            } catch (DbUpdateException ex) when (attempt == 0 && UniqueConstraintDetector.IsUniqueConstraintViolation(ex)) {
                db.ChangeTracker.Clear();
            }
        }

        return await CreateNewAnalysisAsync(request, normalizedKey, requestHash, cancellationToken);
    }

    private async Task<AnalyseTransactionResult> CreateNewAnalysisAsync(
        AnalyseTransactionRequest request,
        string? idempotencyKey,
        string? requestHash,
        CancellationToken cancellationToken
    ) {
        var createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try {
            var entities = await entityResolver.ResolveAndLinkAsync(request, createdAt, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            var context = new RiskSignalContext(
                entities.User,
                request,
                entities.Device,
                entities.Card,
                entities.IpAddress,
                entities.IsNewDevice,
                createdAt
            );

            var signals = await signalBuilder.BuildAsync(context, cancellationToken);
            var score = RiskDecisionService.NormaliseScore(signals);
            var decision = RiskDecisionService.Decide(score);

            var record = CreateTransactionRecord(request, entities, createdAt, score, decision, idempotencyKey, requestHash);
            db.Transactions.Add(record);
            await db.SaveChangesAsync(cancellationToken);

            AddRiskEvents(record.Id, signals);
            AddFraudCaseIfRequired(record.Id, request, decision, createdAt);
            AddAuditLog(record, decision, createdAt);
            AddOutboxMessages(record, signals, decision, createdAt);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return AnalyseTransactionResult.Created(AnalyseTransactionResponseMapper.FromRecord(record, entities.User, signals));
        } catch (DbUpdateException) when (idempotencyKey is not null) {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();

            var existing = await FindExistingByIdempotencyKeyAsync(request.UserId, idempotencyKey, cancellationToken);
            if (existing is not null) {
                var conflict = CheckIdempotencyPayload(existing, requestHash);
                if (conflict is not null) {
                    return AnalyseTransactionResult.Conflict(conflict);
                }

                return AnalyseTransactionResult.Replay(
                    AnalyseTransactionResponseMapper.FromRecord(existing, existing.UserProfile, existing.RiskEvents)
                );
            }

            throw;
        }
    }

    private async Task<TransactionRecord?> FindExistingByIdempotencyKeyAsync(
        Guid userId,
        string? idempotencyKey,
        CancellationToken cancellationToken
    ) {
        if (idempotencyKey is null) {
            return null;
        }

        return await db.Transactions
            .AsNoTracking()
            .Include(x => x.UserProfile)
            .Include(x => x.RiskEvents)
            .FirstOrDefaultAsync(
                x => x.UserProfileId == userId && x.IdempotencyKey == idempotencyKey,
                cancellationToken
            );
    }

    private static string? NormalizeIdempotencyKey(string? value) {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? CheckIdempotencyPayload(TransactionRecord existing, string? requestHash) {
        if (existing.IdempotencyKey is null || requestHash is null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(existing.RequestHash)) {
            return null;
        }

        return string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)
            ? null
            : "The supplied idempotency key was already used with a different transaction payload.";
    }

}
