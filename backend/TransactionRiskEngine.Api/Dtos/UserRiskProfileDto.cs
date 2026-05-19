namespace TransactionRiskEngine.Api.Dtos;

public sealed record UserRiskProfileDto(
    Guid UserId,
    string DisplayName,
    bool IsFlagged,
    int TransactionCount,
    decimal AverageAmount,
    int ReviewCount,
    int BlockedCount,
    DateTimeOffset? LastTransactionAt
);
