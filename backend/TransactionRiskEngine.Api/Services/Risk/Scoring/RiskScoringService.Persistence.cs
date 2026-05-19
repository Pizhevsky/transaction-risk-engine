using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Services.Transactions;

namespace TransactionRiskEngine.Api.Services.Risk;

public sealed partial class RiskScoringService {
    private static TransactionRecord CreateTransactionRecord(
        AnalyseTransactionRequest request,
        ResolvedTransactionEntities entities,
        DateTimeOffset createdAt,
        int score,
        TransactionDecision decision,
        string? idempotencyKey,
        string? requestHash
    ) {
        return new TransactionRecord {
            Id = Guid.NewGuid(),
            UserProfileId = entities.User.Id,
            DeviceId = entities.Device.Id,
            PaymentCardId = entities.Card.Id,
            IpAddressRecordId = entities.IpAddress.Id,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Merchant = request.Merchant.Trim(),
            Successful = request.Successful,
            CreatedAt = createdAt,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            RiskScore = score,
            Decision = decision
        };
    }

    private void AddRiskEvents(Guid transactionId, IReadOnlyList<RiskSignal> signals) {
        foreach (var signal in signals) {
            db.RiskEvents.Add(new RiskEvent {
                Id = Guid.NewGuid(),
                TransactionRecordId = transactionId,
                Code = signal.Code,
                BaseScore = signal.BaseScore,
                Score = signal.Score,
                Reason = signal.Reason,
                Evidence = signal.Evidence
            });
        }
    }

    private void AddFraudCaseIfRequired(
        Guid transactionId,
        AnalyseTransactionRequest request,
        TransactionDecision decision,
        DateTimeOffset createdAt
    ) {
        if (decision is not (TransactionDecision.Review or TransactionDecision.Blocked)) {
            return;
        }

        db.FraudCases.Add(new FraudCase {
            Id = Guid.NewGuid(),
            TransactionRecordId = transactionId,
            Status = FraudCaseStatus.Open,
            Summary = $"{decision} decision for {request.Currency.ToUpperInvariant()} {request.Amount:N2} at {request.Merchant.Trim()}",
            CreatedAt = createdAt
        });
    }

    private void AddAuditLog(TransactionRecord record, TransactionDecision decision, DateTimeOffset createdAt) {
        db.AuditLogs.Add(new AuditLog {
            Id = Guid.NewGuid(),
            Action = "TransactionAnalysed",
            EntityType = nameof(TransactionRecord),
            EntityId = record.Id,
            TransactionRecordId = record.Id,
            CorrelationId = correlationIdAccessor.Current,
            Summary = $"Transaction {record.Id} scored {record.RiskScore} and was {decision}.",
            CreatedAt = createdAt
        });
    }

    private void AddOutboxMessages(
        TransactionRecord record,
        IReadOnlyCollection<RiskSignal> signals,
        TransactionDecision decision,
        DateTimeOffset createdAt
    ) {
        outboxWriter.Add(
            "transaction.analysed",
            new {
                transactionId = record.Id,
                userId = record.UserProfileId,
                score = record.RiskScore,
                decision = decision.ToString(),
                signalCodes = signals.Select(x => x.Code).Distinct().ToArray()
            },
            createdAt
        );

        if (decision is TransactionDecision.Review or TransactionDecision.Blocked) {
            outboxWriter.Add(
                "fraud-case.opened",
                new {
                    transactionId = record.Id,
                    userId = record.UserProfileId,
                    score = record.RiskScore,
                    decision = decision.ToString()
                },
                createdAt
            );
        }
    }
}
