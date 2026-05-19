namespace TransactionRiskEngine.Api.Domain;

public sealed class RiskRule {
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Weight { get; set; }
    public bool Enabled { get; set; } = true;
}
