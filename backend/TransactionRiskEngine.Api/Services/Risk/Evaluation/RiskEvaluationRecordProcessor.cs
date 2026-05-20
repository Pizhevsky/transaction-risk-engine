using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Infrastructure;
using TransactionRiskEngine.Api.Services.Outbox;

namespace TransactionRiskEngine.Api.Services.Risk;

public sealed class RiskEvaluationRecordProcessor(
    ICorrelationIdAccessor correlationIdAccessor
) {
    public async Task<bool> EvaluateAsync(
        TransactionRecord record,
        IReadOnlyDictionary<string, RiskRuleSnapshot> rules,
        string reason,
        DateTimeOffset now,
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        var oldScore = record.RiskScore;
        var oldDecision = record.Decision;

        await UpgradeAmountBaseScoreAsync(db, record, cancellationToken);
        ApplyCurrentRules(record, rules);
        ApplyDecision(record);
        var fraudCaseTransition = ReconcileFraudCase(db, record, now);
        var riskChanged = oldScore != record.RiskScore || oldDecision != record.Decision;

        if (!riskChanged && fraudCaseTransition == FraudCaseTransition.None) {
            return false;
        }

        AddAuditLog(db, record, oldScore, oldDecision, reason, now);
        AddOutboxMessages(db, record, oldScore, oldDecision, riskChanged, fraudCaseTransition, now);
        return true;
    }

    private static void ApplyCurrentRules(
        TransactionRecord record,
        IReadOnlyDictionary<string, RiskRuleSnapshot> rules
    ) {
        foreach (var riskEvent in record.RiskEvents) {
            riskEvent.Score = RiskRuleApplicator.ResolveAppliedScore(
                riskEvent.Code,
                riskEvent.BaseScore,
                rules
            );
        }
    }

    private void ApplyDecision(TransactionRecord record) {
        var signals = record.RiskEvents.Select(x => new RiskSignal(
            x.Code,
            x.BaseScore,
            x.Score,
            x.Reason,
            x.Evidence)
        );

        record.RiskScore = RiskDecisionService.NormaliseScore(signals);
        record.Decision = RiskDecisionService.Decide(record.RiskScore);
    }

    private static FraudCaseTransition ReconcileFraudCase(
        AppDbContext db,
        TransactionRecord record,
        DateTimeOffset now
    ) {
        if (record.Decision is TransactionDecision.Approved) {
            return CloseOpenFraudCaseIfRiskCleared(record, now);
        }

        if (record.Decision is not (TransactionDecision.Review or TransactionDecision.Blocked)) {
            return FraudCaseTransition.None;
        }

        if (record.FraudCase is null) {
            record.FraudCase = new FraudCase {
                Id = Guid.NewGuid(),
                TransactionRecordId = record.Id,
                Status = FraudCaseStatus.Open,
                Summary = $"{record.Decision} decision after rule evaluation for {record.Currency} {record.Amount:N2} at {record.Merchant}",
                CreatedAt = now
            };
            db.FraudCases.Add(record.FraudCase);

            return FraudCaseTransition.Opened;
        }

        if (record.FraudCase.Status is FraudCaseStatus.ClosedApproved or FraudCaseStatus.ClosedBlocked) {
            record.FraudCase.Status = FraudCaseStatus.Open;
            record.FraudCase.ClosedAt = null;
            record.FraudCase.Summary = $"Reopened after rule evaluation changed decision to {record.Decision} for {record.Currency} {record.Amount:N2} at {record.Merchant}";
            return FraudCaseTransition.Reopened;
        }

        return FraudCaseTransition.None;
    }

    private static FraudCaseTransition CloseOpenFraudCaseIfRiskCleared(TransactionRecord record, DateTimeOffset now) {
        if (record.FraudCase is null ||
            record.FraudCase.Status is not (FraudCaseStatus.Open or FraudCaseStatus.Investigating)) {
            return FraudCaseTransition.None;
        }

        record.FraudCase.Status = FraudCaseStatus.ClosedApproved;
        record.FraudCase.ClosedAt = now;
        record.FraudCase.Summary = $"Closed after rule evaluation reduced decision to Approved for {record.Currency} {record.Amount:N2} at {record.Merchant}";
        return FraudCaseTransition.ClosedApproved;
    }

    private static async Task UpgradeAmountBaseScoreAsync(
        AppDbContext db,
        TransactionRecord record,
        CancellationToken cancellationToken
    ) {
        var amountEvents = record.RiskEvents
            .Where(x => x.Code.Equals("HIGH_AMOUNT", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (amountEvents.Count == 0) {
            return;
        }

        var history = await db.Transactions
            .AsNoTracking()
            .Where(x => x.UserProfileId == record.UserProfileId &&
                x.Successful &&
                x.Currency == record.Currency &&
                x.CreatedAt < record.CreatedAt
            )
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var detectedSignal = AmountAnomalyDetector.Detect(record.Amount, history);
        if (detectedSignal is null) {
            return;
        }

        foreach (var amountEvent in amountEvents.Where(x => x.BaseScore < detectedSignal.BaseScore)) {
            amountEvent.BaseScore = detectedSignal.BaseScore;
            amountEvent.Reason = detectedSignal.Reason;
            amountEvent.Evidence = detectedSignal.Evidence;
        }
    }

    private void AddAuditLog(
        AppDbContext db,
        TransactionRecord record,
        int oldScore,
        TransactionDecision oldDecision,
        string reason,
        DateTimeOffset createdAt
    ) {
        db.AuditLogs.Add(new AuditLog {
            Id = Guid.NewGuid(),
            Action = "TransactionRiskEvaluated",
            EntityType = nameof(TransactionRecord),
            EntityId = record.Id,
            TransactionRecordId = record.Id,
            CorrelationId = correlationIdAccessor.Current,
            Summary = $"Rule evaluation changed transaction {record.Id} from {oldScore}/{oldDecision} to {record.RiskScore}/{record.Decision}. Reason: {reason}",
            CreatedAt = createdAt
        });
    }

    private static void AddOutboxMessages(
        AppDbContext db,
        TransactionRecord record,
        int oldScore,
        TransactionDecision oldDecision,
        bool riskChanged,
        FraudCaseTransition fraudCaseTransition,
        DateTimeOffset occurredAt
    ) {
        var writer = new OutboxWriter(db);

        if (riskChanged) {
            writer.Add(
                "transaction.risk-changed",
                new {
                    transactionId = record.Id,
                    userId = record.UserProfileId,
                    previousScore = oldScore,
                    score = record.RiskScore,
                    previousDecision = oldDecision.ToString(),
                    decision = record.Decision.ToString()
                },
                occurredAt
            );
        }

        if (fraudCaseTransition == FraudCaseTransition.None) {
            return;
        }

        writer.Add(
            ResolveFraudCaseEventType(fraudCaseTransition),
            new {
                transactionId = record.Id,
                userId = record.UserProfileId,
                score = record.RiskScore,
                decision = record.Decision.ToString(),
                caseStatus = record.FraudCase?.Status.ToString()
            },
            occurredAt
        );
    }

    private static string ResolveFraudCaseEventType(FraudCaseTransition transition) {
        return transition switch {
            FraudCaseTransition.Opened => "fraud-case.opened",
            FraudCaseTransition.Reopened => "fraud-case.reopened",
            FraudCaseTransition.ClosedApproved => "fraud-case.closed-approved",
            _ => "fraud-case.changed"
        };
    }

    private enum FraudCaseTransition {
        None,
        Opened,
        Reopened,
        ClosedApproved
    }
}
