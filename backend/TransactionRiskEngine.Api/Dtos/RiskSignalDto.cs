namespace TransactionRiskEngine.Api.Dtos;

public sealed record RiskSignalDto(
    string Code,
    int BaseScore,
    int Score,
    string Reason,
    string Evidence
);
