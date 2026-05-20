using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Infrastructure;
using TransactionRiskEngine.Api.Services.Outbox;

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
                x.ReviewNote,
                x.CreatedAt,
                x.ClosedAt
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(cases);
    }

    public static async Task<IResult> UpdateStatusAsync(
        Guid id,
        UpdateFraudCaseStatusRequest request,
        AppDbContext db,
        ICorrelationIdAccessor correlationIdAccessor,
        IOutboxWriter outboxWriter,
        CancellationToken cancellationToken
    ) {
        if (!TryParseStatus(request.Status, out var newStatus)) {
            return Results.ValidationProblem(new Dictionary<string, string[]> {
                [nameof(request.Status)] = ["Status must be Open, Investigating, ClosedApproved, or ClosedBlocked."]
            });
        }

        if (request.Note is { Length: > 500 }) {
            return Results.ValidationProblem(new Dictionary<string, string[]> {
                [nameof(request.Note)] = ["Note must be 500 characters or fewer."]
            });
        }

        var fraudCase = await db.FraudCases
            .Include(x => x.TransactionRecord)
            .ThenInclude(x => x.UserProfile)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (fraudCase is null) {
            return Results.NotFound(new { error = "Fraud case not found" });
        }

        if (!IsAllowedTransition(fraudCase.Status, newStatus)) {
            return Results.Conflict(new {
                error = $"Cannot move fraud case from {fraudCase.Status} to {newStatus}. Open and investigating cases can be closed or moved between review states; closed cases can be reopened to Open or Investigating."
            });
        }

        var oldStatus = fraudCase.Status;
        var now = DateTimeOffset.UtcNow;

        if (oldStatus != newStatus) {
            fraudCase.Status = newStatus;
            fraudCase.ReviewNote = ApplyReviewNote(fraudCase.ReviewNote, request.Note);
            fraudCase.ClosedAt = IsClosedStatus(newStatus) ? now : null;
            AddAuditLog(db, correlationIdAccessor.Current, fraudCase, oldStatus, newStatus, request.Note, now);
            AddStatusChangedOutbox(outboxWriter, correlationIdAccessor.Current, fraudCase, oldStatus, newStatus, now);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(ToDto(fraudCase));
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
            EF.Functions.ILike(x.TransactionRecord.UserProfile.DisplayName, pattern, "\\") ||
            EF.Functions.ILike(x.TransactionRecord.Merchant, pattern, "\\") ||
            EF.Functions.ILike(x.Summary, pattern, "\\") ||
            x.ReviewNote != null && EF.Functions.ILike(x.ReviewNote, pattern, "\\")
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

    private static FraudCaseDto ToDto(FraudCase fraudCase) => new(
        fraudCase.Id,
        fraudCase.TransactionRecordId,
        fraudCase.TransactionRecord.UserProfile.DisplayName,
        fraudCase.TransactionRecord.Amount,
        fraudCase.TransactionRecord.Currency,
        fraudCase.TransactionRecord.RiskScore,
        fraudCase.Status.ToString(),
        fraudCase.Summary,
        fraudCase.ReviewNote,
        fraudCase.CreatedAt,
        fraudCase.ClosedAt
    );

    private static bool TryParseStatus(string? value, out FraudCaseStatus status) {
        status = default;
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        var normalised = value.Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse(normalised, ignoreCase: true, out status) &&
            Enum.IsDefined(typeof(FraudCaseStatus), status);
    }

    private static bool IsAllowedTransition(FraudCaseStatus current, FraudCaseStatus next) {
        if (current == next) {
            return true;
        }

        return current switch {
            FraudCaseStatus.Open => next is FraudCaseStatus.Investigating or FraudCaseStatus.ClosedApproved or FraudCaseStatus.ClosedBlocked,
            FraudCaseStatus.Investigating => next is FraudCaseStatus.Open or FraudCaseStatus.ClosedApproved or FraudCaseStatus.ClosedBlocked,
            FraudCaseStatus.ClosedApproved or FraudCaseStatus.ClosedBlocked => next is FraudCaseStatus.Open or FraudCaseStatus.Investigating,
            _ => false
        };
    }

    private static bool IsClosedStatus(FraudCaseStatus status) =>
        status is FraudCaseStatus.ClosedApproved or FraudCaseStatus.ClosedBlocked;

    private static string? ApplyReviewNote(
        string? existingNote,
        string? requestedNote
    ) {
        if (requestedNote is null) {
            return existingNote;
        }

        var trimmed = requestedNote.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static void AddStatusChangedOutbox(
        IOutboxWriter outboxWriter,
        string? correlationId,
        FraudCase fraudCase,
        FraudCaseStatus oldStatus,
        FraudCaseStatus newStatus,
        DateTimeOffset occurredAt
    ) {
        outboxWriter.Add(
            "fraud-case.status-changed",
            new {
                fraudCaseId = fraudCase.Id,
                transactionId = fraudCase.TransactionRecordId,
                userId = fraudCase.TransactionRecord.UserProfileId,
                previousStatus = oldStatus.ToString(),
                status = newStatus.ToString(),
                closedAt = fraudCase.ClosedAt,
                correlationId
            },
            occurredAt
        );
    }

    private static void AddAuditLog(
        AppDbContext db,
        string? correlationId,
        FraudCase fraudCase,
        FraudCaseStatus oldStatus,
        FraudCaseStatus newStatus,
        string? note,
        DateTimeOffset createdAt
    ) {
        var noteText = string.IsNullOrWhiteSpace(note) ? string.Empty : $" Note: {note.Trim()}";
        db.AuditLogs.Add(new AuditLog {
            Id = Guid.NewGuid(),
            Action = "FraudCaseStatusChanged",
            EntityType = nameof(FraudCase),
            EntityId = fraudCase.Id,
            TransactionRecordId = fraudCase.TransactionRecordId,
            CorrelationId = correlationId,
            Summary = $"Fraud case {fraudCase.Id} changed from {oldStatus} to {newStatus}.{noteText}",
            CreatedAt = createdAt
        });
    }
}

internal sealed class FraudCaseListQuery {
    public string? Search { get; init; }
    public string? Status { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }
}
