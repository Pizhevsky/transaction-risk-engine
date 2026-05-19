namespace TransactionRiskEngine.Api.Domain;

public sealed class PaymentCard {
    public Guid Id { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string Brand { get; set; } = "Unknown";
    public string Last4 { get; set; } = "0000";
    public bool IsFlagged { get; set; }

    public List<UserCard> Users { get; set; } = [];
}
