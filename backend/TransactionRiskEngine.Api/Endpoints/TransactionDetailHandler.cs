using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Services.Graph;

namespace TransactionRiskEngine.Api.Endpoints;

internal static partial class TransactionEndpointHandlers {
    public static async Task<IResult> GetDetailAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        var item = await db.Transactions
            .AsNoTracking()
            .Include(x => x.UserProfile)
            .Include(x => x.Device)
            .Include(x => x.PaymentCard)
            .Include(x => x.IpAddressRecord)
            .Include(x => x.RiskEvents)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return item is null
            ? Results.NotFound()
            : Results.Ok(ToDetailDto(item));
    }

    public static async Task<IResult> GetConnectionsAsync(
        Guid id,
        ITransactionGraphService transactionGraphService,
        CancellationToken cancellationToken
    ) {
        var graph = await transactionGraphService.BuildTransactionGraphAsync(id, cancellationToken);

        return graph is null
            ? Results.NotFound()
            : Results.Ok(graph);
    }

    private static TransactionDetailDto ToDetailDto(TransactionRecord item) {
        return new TransactionDetailDto(
            item.Id,
            item.UserProfileId,
            item.UserProfile.DisplayName,
            item.Amount,
            item.Currency,
            item.Merchant,
            item.Successful,
            item.RiskScore,
            item.Decision.ToString(),
            item.Device?.Fingerprint,
            item.PaymentCard?.Fingerprint,
            item.IpAddressRecord?.Address,
            item.RiskEvents
                .OrderByDescending(x => x.Score)
                .Select(x => new RiskSignalDto(x.Code, x.BaseScore, x.Score, x.Reason, x.Evidence))
                .ToList(),
            item.CreatedAt
        );
    }
}
