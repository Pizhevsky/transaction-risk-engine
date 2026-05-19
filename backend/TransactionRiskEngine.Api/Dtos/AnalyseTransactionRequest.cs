using System.ComponentModel.DataAnnotations;

namespace TransactionRiskEngine.Api.Dtos;

public sealed record AnalyseTransactionRequest(
    Guid UserId,
    decimal Amount,

    [property: Required]
    [property: StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter ISO code.")]
    [property: RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must contain letters only.")]
    string Currency,

    [property: Required]
    [property: StringLength(180, MinimumLength = 1)]
    string Merchant,

    [property: Required]
    [property: StringLength(128, MinimumLength = 1)]
    string CardFingerprint,

    [property: Required]
    [property: RegularExpression("^[0-9]{4}$", ErrorMessage = "CardLast4 must contain exactly four digits.")]
    string CardLast4,

    [property: Required]
    [property: StringLength(128, MinimumLength = 1)]
    string DeviceFingerprint,

    [property: Required]
    [property: StringLength(64, MinimumLength = 1)]
    string IpAddress,

    bool Successful,
    DateTimeOffset? CreatedAt = null,

    [property: StringLength(96, MinimumLength = 8)]
    [property: RegularExpression("^[A-Za-z0-9._:-]+$", ErrorMessage = "IdempotencyKey may contain letters, digits, dot, colon, underscore, or dash only.")]
    string? IdempotencyKey = null
);
