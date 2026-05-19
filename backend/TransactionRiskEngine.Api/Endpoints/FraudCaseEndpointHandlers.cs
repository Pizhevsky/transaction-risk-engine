using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Endpoints;

internal static class FraudCaseEndpointHandlers {
    public static async Task<IResult> ListAsync(
        AppDbContext db,
        HttpResponse response,
        [AsParameters] FraudCaseListQuery query,
        CancellationToken cancellationToken
    ) {
        var (take, skip) = EndpointQueryHelpers.ResolvePaging(query.Limit, query.Offset);
        var casesQuery = ApplyFilters(db.FraudCases.AsNoTracking(), query.Search, query.Status);
        var total = await casesQuery.CountAsync(cancellationToken);

        EndpointQueryHelpers.WritePaginationHeaders(response, total, take, skip);

        var cases = await casesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => new FraudCaseDto(
                x.Id,
                x.TransactionRecordId,
                x.TransactionRecord.UserProfile.DisplayName,
                x.TransactionRecord.Amount,
                x.TransactionRecord.Currency,
                x.TransactionRecord.RiskScore,
                x.Status.ToString(),
                x.Summary,
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(cases);
    }

    private static IQueryable<FraudCase> ApplyFilters(
        IQueryable<FraudCase> query,
        string? search,
        string? status
    ) {
        query = ApplySearch(query, search);
        return ApplyStatus(query, status);
    }

    private static IQueryable<FraudCase> ApplySearch(
        IQueryable<FraudCase> query,
        string? search
    ) {
        if (string.IsNullOrWhiteSpace(search)) {
            return query;
        }

        var pattern = EndpointQueryHelpers.ContainsPattern(search);
        return query.Where(x =>
            EF.Functions.Like(x.TransactionRecord.UserProfile.DisplayName.ToLower(), pattern, "\\") ||
            EF.Functions.Like(x.TransactionRecord.Merchant.ToLower(), pattern, "\\") ||
            EF.Functions.Like(x.Summary.ToLower(), pattern, "\\")
        );
    }

    private static IQueryable<FraudCase> ApplyStatus(
        IQueryable<FraudCase> query,
        string? status
    ) {
        return status?.ToLowerInvariant() switch {
            "open" => query.Where(x => x.Status == FraudCaseStatus.Open),
            "investigating" => query.Where(x => x.Status == FraudCaseStatus.Investigating),
            "closed" => query.Where(x =>
                x.Status == FraudCaseStatus.ClosedApproved ||
                x.Status == FraudCaseStatus.ClosedBlocked),
            "closed-approved" => query.Where(x => x.Status == FraudCaseStatus.ClosedApproved),
            "closed-blocked" => query.Where(x => x.Status == FraudCaseStatus.ClosedBlocked),
            _ => query
        };
    }

}

internal sealed class FraudCaseListQuery {
    public string? Search { get; init; }
    public string? Status { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}
