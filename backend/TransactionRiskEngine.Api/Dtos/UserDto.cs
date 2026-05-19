namespace TransactionRiskEngine.Api.Dtos;

public sealed record UserDto(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsFlagged
);
