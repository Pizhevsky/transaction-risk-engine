using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Infrastructure;
using TransactionRiskEngine.Api.Services.Outbox;
using TransactionRiskEngine.Api.Services.Risk;

namespace TransactionRiskEngine.Api.Endpoints;

internal static class RuleEndpointHandlers {
    public static async Task<IResult> ListAsync(AppDbContext db, CancellationToken cancellationToken) {
        var rules = await db.RiskRules
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new RiskRuleDto(x.Code, x.Description, x.Weight, x.Enabled))
            .ToListAsync(cancellationToken);

        return Results.Ok(rules);
    }

    public static async Task<IResult> UpdateAsync(
        string code,
        UpdateRiskRuleRequest request,
        AppDbContext db,
        ICorrelationIdAccessor correlationIdAccessor,
        IOutboxWriter outboxWriter,
        IRiskRuleCatalog riskRuleCatalog,
        CancellationToken cancellationToken
    ) {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var validationError = ValidateUpdateRequest(request);
        if (validationError is { } error) {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { [error.Field] = [error.Message] },
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Risk rule validation failed");
        }

        var rule = await db.RiskRules.FirstOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
        if (rule is null) {
            return Results.NotFound(new { error = $"Risk rule '{normalizedCode}' was not found." });
        }

        var before = new RuleSnapshot(rule.Description, rule.Weight, rule.Enabled);
        rule.Description = request.Description?.Trim() ?? rule.Description;
        rule.Weight = request.Weight;
        rule.Enabled = request.Enabled;

        var now = DateTimeOffset.UtcNow;
        AddAuditLog(db, correlationIdAccessor.Current, rule, before, now);
        AddOutboxMessage(outboxWriter, rule, before, now);

        try {
            await db.SaveChangesAsync(cancellationToken);
        } finally {
            riskRuleCatalog.Invalidate();
        }

        return Results.Ok(new RiskRuleUpdateResponse(
            rule.Code,
            rule.Description,
            rule.Weight,
            rule.Enabled,
            "Rule updated. Existing risk events can be evaluated with POST /api/rules/evaluate."
        ));
    }

    public static async Task<IResult> EvaluateAsync(
        RiskEvaluationRequest request,
        IRiskEvaluationService service,
        CancellationToken cancellationToken
    ) {
        var result = await service.EvaluateRecentTransactionsAsync(
            request.BatchSize,
            string.IsNullOrWhiteSpace(request.Reason) ? "Manual rule evaluation" : request.Reason.Trim(),
            cancellationToken);

        return Results.Ok(new RiskEvaluationResponse(result.JobId, result.ProcessedCount, result.ChangedCount));
    }

    public static async Task<IResult> ListJobsAsync(AppDbContext db, CancellationToken cancellationToken) {
        var jobs = await db.RiskEvaluationJobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new {
                x.Id,
                x.Reason,
                x.RequestedBatchSize,
                x.ProcessedCount,
                x.ChangedCount,
                x.CreatedAt,
                x.CompletedAt
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(jobs);
    }

    private static void AddAuditLog(
        AppDbContext db,
        string? correlationId,
        RiskRule rule,
        RuleSnapshot before,
        DateTimeOffset now
    ) {
        db.AuditLogs.Add(new AuditLog {
            Id = Guid.NewGuid(),
            Action = "RiskRuleUpdated",
            EntityType = nameof(RiskRule),
            EntityCode = rule.Code,
            CorrelationId = correlationId,
            Summary = $"Rule {rule.Code} changed from weight={before.Weight}, enabled={before.Enabled} to weight={rule.Weight}, enabled={rule.Enabled}.",
            CreatedAt = now
        });
    }

    private static void AddOutboxMessage(
        IOutboxWriter outboxWriter,
        RiskRule rule,
        RuleSnapshot before,
        DateTimeOffset now
    ) {
        outboxWriter.Add(
            "risk-rule.updated",
            new {
                code = rule.Code,
                before,
                after = new { rule.Description, rule.Weight, rule.Enabled }
            },
            now
        );
    }

    private static (string Field, string Message)? ValidateUpdateRequest(UpdateRiskRuleRequest request) {
        if (request.Weight is < 0 or > 100) {
            return (nameof(request.Weight), "Weight must be between 0 and 100.");
        }

        if (request.Description is not null && request.Description.Trim().Length > 300) {
            return (nameof(request.Description), "Description must be 300 characters or fewer.");
        }

        return null;
    }

    private sealed record RuleSnapshot(string Description, int Weight, bool Enabled);
}
