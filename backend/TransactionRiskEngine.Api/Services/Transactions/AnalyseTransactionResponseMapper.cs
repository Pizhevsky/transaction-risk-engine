using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Services.Risk;

namespace TransactionRiskEngine.Api.Services.Transactions;

public static class AnalyseTransactionResponseMapper {
    public static AnalyseTransactionResponse FromRecord(
        TransactionRecord record,
        UserProfile user,
        IEnumerable<RiskEvent> events
    ) {
        return Create(
            record,
            user,
            events
                .OrderByDescending(x => x.Score)
                .Select(x => new RiskSignalDto(x.Code, x.BaseScore, x.Score, x.Reason, x.Evidence))
        );
    }

    public static AnalyseTransactionResponse FromRecord(
        TransactionRecord record,
        UserProfile user,
        IReadOnlyList<RiskSignal> signals
    ) {
        return Create(
            record,
            user,
            signals.Select(x => new RiskSignalDto(x.Code, x.BaseScore, x.Score, x.Reason, x.Evidence))
        );
    }

    private static AnalyseTransactionResponse Create(
        TransactionRecord record,
        UserProfile user,
        IEnumerable<RiskSignalDto> signals
    ) {
        return new AnalyseTransactionResponse(
            record.Id,
            user.Id,
            user.DisplayName,
            record.Amount,
            record.Currency,
            record.Merchant,
            record.RiskScore,
            record.Decision.ToString(),
            signals.ToList(),
            record.CreatedAt
        );
    }
}
