namespace TransactionRiskEngine.Api.Dtos;

public sealed record TransactionSummaryDto(
    Guid Id,
    Guid UserId,
    string UserName,
    decimal Amount,
    string Currency,
    string Merchant,
    bool Successful,
    int RiskScore,
    string Decision,
    string? TopReason,
    DateTimeOffset CreatedAt
);
