using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Services.Transactions;

namespace TransactionRiskEngine.Api.Services.Risk;

public interface IRiskScoringService {
    Task<AnalyseTransactionResult> AnalyseAndStoreAsync(
        AnalyseTransactionRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken
    );
}
