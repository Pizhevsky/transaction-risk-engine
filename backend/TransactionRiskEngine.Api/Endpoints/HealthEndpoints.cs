using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Endpoints;

public static class HealthEndpoints {
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app) {
        var health = app.MapGroup("/health").WithTags("Health");
        health.MapGet("/live", GetLiveHealth);
        health.MapGet("/ready", GetReadyHealthAsync);
        health.MapGet("/status", GetHealthStatusAsync);

        return app;
    }

    private static IResult GetLiveHealth() {
        return Results.Ok(new {
            status = "ok",
            service = "TransactionRiskEngine"
        });
    }

    private static async Task<IResult> GetReadyHealthAsync(
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        if (!canConnect) {
            return Results.Problem(
                title: "Database unavailable",
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }

        var outbox = await LoadOutboxHealthAsync(db, cancellationToken);
        var oldestPendingAgeSeconds = CalculatePendingAgeSeconds(outbox.OldestPendingAt);
        var isDegraded = IsOutboxDegraded(outbox.Failed, oldestPendingAgeSeconds);

        return Results.Json(
            new {
                status = isDegraded ? "degraded" : "ready",
                database = "reachable",
                outbox = new
                {
                    pending = outbox.Pending,
                    processing = outbox.Processing,
                    failed = outbox.Failed,
                    oldestPendingAgeSeconds
                }
            },
            statusCode: ToReadyStatusCode(isDegraded)
        );
    }

    private static async Task<IResult> GetHealthStatusAsync(
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        var metrics = canConnect
            ? await LoadHealthStatusMetricsAsync(db, cancellationToken)
            : HealthStatusMetrics.Empty;

        return Results.Ok(new {
            status = canConnect ? "ok" : "database-unavailable",
            database = canConnect ? "reachable" : "unreachable",
            riskRules = metrics.RiskRules,
            outbox = new { 
                pending = metrics.PendingOutbox,
                failed = metrics.FailedOutbox
            }
        });
    }

    private static async Task<OutboxHealthSnapshot> LoadOutboxHealthAsync(
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        var outbox = await db.OutboxMessages
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new OutboxHealthSnapshot(
                group.Count(x => x.Status == OutboxMessageStatus.Pending),
                group.Count(x => x.Status == OutboxMessageStatus.Processing),
                group.Count(x => x.Status == OutboxMessageStatus.Failed),
                group
                    .Where(x => x.Status == OutboxMessageStatus.Pending)
                    .Min(x => (DateTimeOffset?)x.OccurredAt)))
            .FirstOrDefaultAsync(cancellationToken);

        return outbox ?? OutboxHealthSnapshot.Empty;
    }

    private static async Task<HealthStatusMetrics> LoadHealthStatusMetricsAsync(
        AppDbContext db,
        CancellationToken cancellationToken
    ) {
        var rules = await db.RiskRules.AsNoTracking().CountAsync(cancellationToken);
        var pendingOutbox = await db.OutboxMessages
            .AsNoTracking()
            .CountAsync(x => x.Status == OutboxMessageStatus.Pending, cancellationToken);
        var failedOutbox = await db.OutboxMessages
            .AsNoTracking()
            .CountAsync(x => x.Status == OutboxMessageStatus.Failed, cancellationToken);

        return new HealthStatusMetrics(rules, pendingOutbox, failedOutbox);
    }

    private static int CalculatePendingAgeSeconds(DateTimeOffset? oldestPendingAt) {
        return oldestPendingAt is null
            ? 0
            : (int)Math.Max(0, (DateTimeOffset.UtcNow - oldestPendingAt.Value).TotalSeconds);
    }

    private static bool IsOutboxDegraded(int failed, int oldestPendingAgeSeconds) {
        return failed > 0 || oldestPendingAgeSeconds > 300;
    }

    private static int ToReadyStatusCode(bool isDegraded) {
        return isDegraded
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;
    }

    private sealed record OutboxHealthSnapshot(
        int Pending,
        int Processing,
        int Failed,
        DateTimeOffset? OldestPendingAt)
    {
        public static readonly OutboxHealthSnapshot Empty = new(0, 0, 0, null);
    }

    private sealed record HealthStatusMetrics(
        int RiskRules,
        int PendingOutbox,
        int FailedOutbox)
    {
        public static readonly HealthStatusMetrics Empty = new(0, 0, 0);
    }
}
