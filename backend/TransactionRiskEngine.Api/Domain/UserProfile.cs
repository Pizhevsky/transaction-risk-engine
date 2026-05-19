namespace TransactionRiskEngine.Api.Domain;

public sealed class UserProfile {
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsFlagged { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<TransactionRecord> Transactions { get; set; } = [];
    public List<UserDevice> Devices { get; set; } = [];
    public List<UserCard> Cards { get; set; } = [];
    public List<UserIpAddress> IpAddresses { get; set; } = [];
}
