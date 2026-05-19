using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Services.Risk;
using TransactionRiskEngine.Api.Services.Transactions;
using TransactionRiskEngine.Api.Validation;

namespace TransactionRiskEngine.Api.Endpoints;

internal static partial class TransactionEndpointHandlers {
    public static async Task<IResult> AnalyseAsync(
        AnalyseTransactionRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        AppDbContext db,
        IRiskScoringService riskScoringService,
        HttpResponse response,
        CancellationToken cancellationToken) {
        var validationError = ValidateAnalyseRequest(request, idempotencyKey);
        if (validationError is not null) {
            return validationError;
        }

        var effectiveIdempotencyKey = ResolveIdempotencyKey(idempotencyKey, request.IdempotencyKey);
        if (!await UserExistsAsync(db, request.UserId, cancellationToken)) {
            return Results.Problem(
                title: "User not found",
                detail: $"No user profile exists for id {request.UserId}.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        var result = await riskScoringService.AnalyseAndStoreAsync(request, effectiveIdempotencyKey, cancellationToken);
        return ToAnalyseResult(result, response);
    }

    private static IResult? ValidateAnalyseRequest(
        AnalyseTransactionRequest request,
        string? idempotencyKey
    ) {
        var validation = AnalyseTransactionRequestValidator.Validate(request);
        if (!validation.IsValid) {
            return Results.ValidationProblem(
                validation.Errors,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Transaction request validation failed"
            );
        }

        var idempotencyAgreement = ValidateIdempotencyAgreement(idempotencyKey, request.IdempotencyKey);
        return idempotencyAgreement.IsValid
            ? null
            : Results.ValidationProblem(
                idempotencyAgreement.Errors,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Idempotency key validation failed"
            );
    }

    private static async Task<bool> UserExistsAsync(
        AppDbContext db,
        Guid userId,
        CancellationToken cancellationToken
    ) {
        return await db.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);
    }

    private static IResult ToAnalyseResult(
        AnalyseTransactionResult result,
        HttpResponse response
    ) {
        if (result.HasConflict) {
            return Results.Conflict(new ProblemDetails {
                Title = "Idempotency key conflict",
                Detail = result.ConflictDetail,
                Status = StatusCodes.Status409Conflict
            });
        }

        if (result.Response is null) {
            return Results.Problem(
                title: "Transaction analysis did not produce a response.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        if (result.IsReplay) {
            response.Headers["X-Idempotent-Replay"] = "true";
            return Results.Ok(result.Response);
        }

        return Results.Created($"/api/transactions/{result.Response.TransactionId}", result.Response);
    }

    private static string? ResolveIdempotencyKey(string? headerValue, string? bodyValue) {
        return !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.Trim()
            : string.IsNullOrWhiteSpace(bodyValue) ? null : bodyValue.Trim();
    }

    private static RequestValidationResult ValidateIdempotencyAgreement(string? headerValue, string? bodyValue) {
        var headerValidation = AnalyseTransactionRequestValidator.ValidateIdempotencyKey(headerValue);
        if (!headerValidation.IsValid) {
            return headerValidation;
        }

        if (string.IsNullOrWhiteSpace(headerValue) || string.IsNullOrWhiteSpace(bodyValue)) {
            return RequestValidationResult.Success();
        }

        return string.Equals(headerValue.Trim(), bodyValue.Trim(), StringComparison.Ordinal)
            ? RequestValidationResult.Success()
            : RequestValidationResult.Failure(new Dictionary<string, List<string>> {
                ["IdempotencyKey"] = ["Header and body idempotency keys must match when both are supplied."]
              });
    }
}
