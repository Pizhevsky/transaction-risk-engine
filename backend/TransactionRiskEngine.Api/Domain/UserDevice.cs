namespace TransactionRiskEngine.Api.Domain;

public sealed class UserDevice {
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = default!;

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = default!;

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
