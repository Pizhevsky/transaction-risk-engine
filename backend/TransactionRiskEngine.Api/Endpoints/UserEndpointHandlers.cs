using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Services.Graph;

namespace TransactionRiskEngine.Api.Endpoints;

internal static class UserEndpointHandlers {
    public static async Task<IResult> ListAsync(
        AppDbContext db,
        HttpResponse response,
        [AsParameters] UserListQuery query,
        CancellationToken cancellationToken
    ) {
        var (take, skip) = EndpointQueryHelpers.ResolvePaging(query.Limit, query.Offset);
        var usersQuery = ApplySearch(db.Users.AsNoTracking(), query.Search);
        var total = await usersQuery.CountAsync(cancellationToken);

        EndpointQueryHelpers.WritePaginationHeaders(response, total, take, skip);

        var users = await usersQuery
            .OrderBy(x => x.DisplayName)
            .Skip(skip)
            .Take(take)
            .Select(x => new UserDto(x.Id, x.DisplayName, x.Email, x.IsFlagged))
            .ToListAsync(cancellationToken);

        return Results.Ok(users);
    }

    public static async Task<IResult> GetRiskProfileAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        var user = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new UserProfileSummary(x.Id, x.DisplayName, x.IsFlagged))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null) {
            return Results.NotFound();
        }

        var summary = await LoadRiskSummaryAsync(db, id, cancellationToken);

        return Results.Ok(new UserRiskProfileDto(
            user.Id,
            user.DisplayName,
            user.IsFlagged,
            summary?.TransactionCount ?? 0,
            summary?.AverageAmount ?? 0,
            summary?.ReviewCount ?? 0,
            summary?.BlockedCount ?? 0,
            summary?.LastTransactionAt
        ));
    }

    public static async Task<IResult> GetConnectionsAsync(
        Guid id,
        AppDbContext db,
        IUserGraphService userGraphService,
        CancellationToken cancellationToken
    ) {
        var exists = await db.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!exists) {
            return Results.NotFound();
        }

        var graph = await userGraphService.BuildGraphAsync(id, cancellationToken);

        return Results.Ok(graph);
    }

    private static IQueryable<UserProfile> ApplySearch(
        IQueryable<UserProfile> query,
        string? search
    ) {
        if (string.IsNullOrWhiteSpace(search)) {
            return query;
        }

        var pattern = EndpointQueryHelpers.ContainsPattern(search);
        return query.Where(x =>
            EF.Functions.Like(x.DisplayName.ToLower(), pattern, "\\") ||
            EF.Functions.Like(x.Email.ToLower(), pattern, "\\")
        );
    }

    private static async Task<UserRiskSummary?> LoadRiskSummaryAsync(
        AppDbContext db,
        Guid userId,
        CancellationToken cancellationToken
    ) {
        return await db.Transactions
            .AsNoTracking()
            .Where(x => x.UserProfileId == userId)
            .GroupBy(_ => 1)
            .Select(group => new UserRiskSummary(
                group.Count(),
                group.Average(x => (decimal?)x.Amount) ?? 0,
                group.Count(x => x.Decision == TransactionDecision.Review),
                group.Count(x => x.Decision == TransactionDecision.Blocked),
                group.Max(x => (DateTimeOffset?)x.CreatedAt)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed record UserProfileSummary(
        Guid Id,
        string DisplayName,
        bool IsFlagged);

    private sealed record UserRiskSummary(
        int TransactionCount,
        decimal AverageAmount,
        int ReviewCount,
        int BlockedCount,
        DateTimeOffset? LastTransactionAt);
}

internal sealed class UserListQuery {
    public string? Search { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}
