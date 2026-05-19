using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Endpoints;

internal static partial class TransactionEndpointHandlers {
    public static async Task<IResult> ListAsync(
        AppDbContext db,
        HttpResponse response,
        [AsParameters] TransactionListQuery query,
        CancellationToken cancellationToken
    ) {
        var (take, skip) = EndpointQueryHelpers.ResolvePaging(query.Limit, query.Offset);
        var transactions = ApplyFilters(db.Transactions.AsNoTracking(), query.Search, query.RiskLevel, query.Status);
        var total = await transactions.CountAsync(cancellationToken);

        EndpointQueryHelpers.WritePaginationHeaders(response, total, take, skip);

        var results = await transactions
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => new TransactionSummaryDto(
                x.Id,
                x.UserProfileId,
                x.UserProfile.DisplayName,
                x.Amount,
                x.Currency,
                x.Merchant,
                x.Successful,
                x.RiskScore,
                x.Decision.ToString(),
                x.RiskEvents.OrderByDescending(e => e.Score).Select(e => e.Reason).FirstOrDefault(),
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(results);
    }

    private static IQueryable<TransactionRecord> ApplyFilters(
        IQueryable<TransactionRecord> query,
        string? search,
        string? riskLevel,
        string? status
    ) {
        query = ApplySearch(query, search);
        query = ApplyRiskLevel(query, riskLevel);
        return ApplyStatus(query, status);
    }

    private static IQueryable<TransactionRecord> ApplySearch(
        IQueryable<TransactionRecord> query,
        string? search
    ) {
        if (string.IsNullOrWhiteSpace(search)) {
            return query;
        }

        var pattern = EndpointQueryHelpers.ContainsPattern(search);
        return query.Where(x =>
            EF.Functions.Like(x.UserProfile.DisplayName.ToLower(), pattern, "\\") ||
            EF.Functions.Like(x.Merchant.ToLower(), pattern, "\\") ||
            EF.Functions.Like(x.Currency.ToLower(), pattern, "\\")
        );
    }

    private static IQueryable<TransactionRecord> ApplyRiskLevel(
        IQueryable<TransactionRecord> query,
        string? riskLevel
    ) {
        return riskLevel?.ToLowerInvariant() switch {
            "approved" => query.Where(x => x.Decision == TransactionDecision.Approved),
            "review" => query.Where(x => x.Decision == TransactionDecision.Review),
            "blocked" => query.Where(x => x.Decision == TransactionDecision.Blocked),
            _ => query
        };
    }

    private static IQueryable<TransactionRecord> ApplyStatus(
        IQueryable<TransactionRecord> query,
        string? status
    ) {
        return status?.ToLowerInvariant() switch {
            "success" => query.Where(x => x.Successful),
            "failed" => query.Where(x => !x.Successful),
            _ => query
        };
    }

}

internal sealed class TransactionListQuery {
    public string? Search { get; init; }
    public string? RiskLevel { get; init; }
    public string? Status { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}
