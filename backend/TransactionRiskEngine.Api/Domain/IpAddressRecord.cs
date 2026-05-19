namespace TransactionRiskEngine.Api.Domain;

public sealed class IpAddressRecord {
    public Guid Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool IsFlagged { get; set; }

    public List<UserIpAddress> Users { get; set; } = [];
}
