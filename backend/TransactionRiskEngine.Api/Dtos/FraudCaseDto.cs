namespace TransactionRiskEngine.Api.Dtos;

public sealed record FraudCaseDto(
    Guid Id,
    Guid TransactionId,
    string UserName,
    decimal Amount,
    string Currency,
    int RiskScore,
    string Status,
    string Summary,
    DateTimeOffset CreatedAt
);
