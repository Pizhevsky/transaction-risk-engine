namespace TransactionRiskEngine.Api.Dtos;

public sealed record AnalyseTransactionResponse(
    Guid TransactionId,
    Guid UserId,
    string UserName,
    decimal Amount,
    string Currency,
    string Merchant,
    int RiskScore,
    string Decision,
    IReadOnlyList<RiskSignalDto> Signals,
    DateTimeOffset CreatedAt
);
