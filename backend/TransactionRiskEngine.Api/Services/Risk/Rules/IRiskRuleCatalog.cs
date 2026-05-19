namespace TransactionRiskEngine.Api.Services.Risk;

public interface IRiskRuleCatalog {
    Task<IReadOnlyDictionary<string, RiskRuleSnapshot>> GetRulesAsync(CancellationToken cancellationToken);
    void Invalidate();
}
