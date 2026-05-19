using System.ComponentModel.DataAnnotations;
using System.Net;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Validation;

public static class AnalyseTransactionRequestValidator {
    public static RequestValidationResult Validate(AnalyseTransactionRequest request) {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        AddDataAnnotationErrors(request, errors);
        AddDomainErrors(request, errors);

        return errors.Count == 0
            ? RequestValidationResult.Success()
            : RequestValidationResult.Failure(errors);
    }

    private static void AddDataAnnotationErrors(
        AnalyseTransactionRequest request,
        Dictionary<string, List<string>> errors
    ) {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            request,
            context,
            results,
            validateAllProperties: true
        );

        foreach (var result in results) {
            var members = result.MemberNames.Any()
                ? result.MemberNames
                : [string.Empty];

            foreach (var member in members) {
                Add(errors, member, result.ErrorMessage ?? "Invalid value.");
            }
        }
    }

    private static void AddDomainErrors(
        AnalyseTransactionRequest request,
        Dictionary<string, List<string>> errors
    ) {
        if (request.UserId == Guid.Empty) {
            Add(errors, nameof(request.UserId), "UserId is required.");
        }

        if (request.Amount <= 0) {
            Add(errors, nameof(request.Amount), "Amount must be greater than zero.");
        }

        if (!string.IsNullOrWhiteSpace(request.IpAddress) && !IPAddress.TryParse(request.IpAddress, out _)) {
            Add(errors, nameof(request.IpAddress), "IpAddress must be a valid IPv4 or IPv6 address.");
        }

        if (request.CreatedAt is not null && request.CreatedAt.Value > DateTimeOffset.UtcNow.AddMinutes(5)) {
            Add(errors, nameof(request.CreatedAt), "CreatedAt cannot be more than five minutes in the future.");
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)) {
            AddIdempotencyKeyErrors(request.IdempotencyKey, errors);
        }
    }

    public static RequestValidationResult ValidateIdempotencyKey(string? value) {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(value)) {
            AddIdempotencyKeyErrors(value, errors);
        }

        return errors.Count == 0
            ? RequestValidationResult.Success()
            : RequestValidationResult.Failure(errors);
    }

    private static void AddIdempotencyKeyErrors(string value, Dictionary<string, List<string>> errors) {
        var trimmed = value.Trim();

        if (trimmed.Length is < 8 or > 96) {
            Add(errors, "IdempotencyKey", "IdempotencyKey must be between 8 and 96 characters.");
        }

        if (!trimmed.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or ':' or '-')) {
            Add(errors, "IdempotencyKey", "IdempotencyKey may contain letters, digits, dot, colon, underscore, or dash only.");
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message) {
        key = string.IsNullOrWhiteSpace(key) ? "request" : key;

        if (!errors.TryGetValue(key, out var messages)) {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }
}
