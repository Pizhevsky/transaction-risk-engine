namespace TransactionRiskEngine.Api.Domain;

public sealed class UserIpAddress {
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = default!;

    public Guid IpAddressRecordId { get; set; }
    public IpAddressRecord IpAddressRecord { get; set; } = default!;

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
