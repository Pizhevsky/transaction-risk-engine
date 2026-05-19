namespace TransactionRiskEngine.Api.Domain;

public sealed class UserCard {
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = default!;

    public Guid PaymentCardId { get; set; }
    public PaymentCard PaymentCard { get; set; } = default!;

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
