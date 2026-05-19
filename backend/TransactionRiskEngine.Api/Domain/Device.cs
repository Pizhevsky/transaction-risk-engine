namespace TransactionRiskEngine.Api.Domain;

public sealed class Device {
    public Guid Id { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string Label { get; set; } = "Unknown device";
    public bool IsFlagged { get; set; }

    public List<UserDevice> Users { get; set; } = [];
}
