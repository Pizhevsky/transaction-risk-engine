namespace TransactionRiskEngine.Api.Dtos;

public sealed record UpdateFraudCaseStatusRequest(
    string Status,
    string? Note
);
