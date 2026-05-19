using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Risk;

public static class AmountAnomalyDetector {
    public static RiskSignal? Detect(decimal amount, IReadOnlyCollection<TransactionRecord> successfulHistory) {
        var averageAmount = successfulHistory.Count == 0
            ? 0
            : successfulHistory.Average(x => x.Amount);

        if (successfulHistory.Count >= 3 && amount > averageAmount * 3 && amount >= 300) {
            var score = ScoreEstablishedHistoryAmount(amount, averageAmount);
            return new RiskSignal(
                "HIGH_AMOUNT",
                score,
                "High amount compared with user history.",
                $"Current amount {amount:N2}; recent average {averageAmount:N2}."
            );
        }

        if (successfulHistory.Count < 3 && amount >= 1000) {
            var score = ScoreLimitedHistoryAmount(amount);
            return new RiskSignal(
                "HIGH_AMOUNT",
                score,
                "High amount with limited history.",
                "The user does not yet have enough successful history for a reliable baseline."
            );
        }

        return null;
    }

    private static int ScoreEstablishedHistoryAmount(decimal amount, decimal averageAmount) {
        if (averageAmount <= 0) {
            return ScoreLimitedHistoryAmount(amount);
        }

        var ratio = amount / averageAmount;
        if (ratio >= 500 || amount >= 1_000_000) {
            return 85;
        }

        if (ratio >= 100 || amount >= 100_000) {
            return 70;
        }

        if (ratio >= 20 || amount >= 10_000) {
            return 50;
        }

        return 30;
    }

    private static int ScoreLimitedHistoryAmount(decimal amount) {
        if (amount >= 1_000_000) {
            return 85;
        }

        if (amount >= 100_000) {
            return 65;
        }

        if (amount >= 10_000) {
            return 45;
        }

        return 20;
    }
}
