namespace TransactionRiskEngine.Api.Dtos;

public sealed record TransactionDetailDto(
    Guid Id,
    Guid UserId,
    string UserName,
    decimal Amount,
    string Currency,
    string Merchant,
    bool Successful,
    int RiskScore,
    string Decision,
    string? DeviceFingerprint,
    string? CardFingerprint,
    string? IpAddress,
    IReadOnlyList<RiskSignalDto> Signals,
    DateTimeOffset CreatedAt
);
