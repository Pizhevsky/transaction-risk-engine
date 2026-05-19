using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public interface IOutboxClaimService {
    Task<int> RequeueStaleProcessingMessagesAsync(AppDbContext db, CancellationToken cancellationToken);
    Task<List<OutboxMessage>> ClaimBatchAsync(AppDbContext db, string workerId, CancellationToken cancellationToken);
}
