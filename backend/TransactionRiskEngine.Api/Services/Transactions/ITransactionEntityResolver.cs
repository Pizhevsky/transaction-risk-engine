using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Transactions;

public interface ITransactionEntityResolver {
    Task<ResolvedTransactionEntities> ResolveAndLinkAsync(
        AnalyseTransactionRequest request,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken
    );
}
