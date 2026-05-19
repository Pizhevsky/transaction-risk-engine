namespace TransactionRiskEngine.Api.Services.Risk;

public interface IRiskEvaluationService {
    Task<RiskEvaluationResult> EvaluateRecentTransactionsAsync(
        int batchSize,
        string reason,
        CancellationToken cancellationToken
    );
}
