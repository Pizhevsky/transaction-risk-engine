namespace TransactionRiskEngine.Api.Services.Risk;

public static class VelocityRiskSignalFactory {
    public static RiskSignal? FromRecentTransactionCount(int countLastTenMinutes) {
        return countLastTenMinutes >= 4
            ? new RiskSignal(
                "VELOCITY_SPIKE",
                20,
                "Transaction velocity spike.",
                $"{countLastTenMinutes} transactions in the last 10 minutes."
              )
            : null;
    }

    public static RiskSignal? FromFailedAttempts(
        int failedAttemptsLastFifteenMinutes,
        bool currentTransactionSuccessful
    ) {
        if (failedAttemptsLastFifteenMinutes == 0) {
            return null;
        }

        if (failedAttemptsLastFifteenMinutes < 2 && currentTransactionSuccessful) {
            return null;
        }

        return new RiskSignal(
            "FAILED_ATTEMPTS",
            currentTransactionSuccessful ? 20 : 25,
            "Recent failed payment behaviour.",
            BuildFailedAttemptEvidence(failedAttemptsLastFifteenMinutes, currentTransactionSuccessful));
    }

    private static string BuildFailedAttemptEvidence(
        int failedAttemptsLastFifteenMinutes,
        bool currentTransactionSuccessful
    ) {
        var priorText = failedAttemptsLastFifteenMinutes == 1
            ? "1 failed attempt in the last 15 minutes"
            : $"{failedAttemptsLastFifteenMinutes} failed attempts in the last 15 minutes";

        return currentTransactionSuccessful
            ? $"{priorText}."
            : $"{priorText}, followed by this unsuccessful transaction.";
    }
}
