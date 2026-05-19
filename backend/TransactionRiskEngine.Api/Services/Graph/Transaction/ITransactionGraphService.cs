using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Graph;

public interface ITransactionGraphService {
    Task<GraphResponseDto?> BuildTransactionGraphAsync(Guid transactionId, CancellationToken cancellationToken);
}
