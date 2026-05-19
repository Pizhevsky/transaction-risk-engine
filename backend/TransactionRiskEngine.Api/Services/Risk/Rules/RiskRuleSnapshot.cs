namespace TransactionRiskEngine.Api.Services.Risk;

public sealed record RiskRuleSnapshot(
    string Code,
    string Description,
    int Weight,
    bool Enabled
);
