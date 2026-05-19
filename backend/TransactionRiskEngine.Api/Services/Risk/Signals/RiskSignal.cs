namespace TransactionRiskEngine.Api.Services.Risk;

public sealed record RiskSignal(
    string Code,
    int BaseScore,
    int Score,
    string Reason,
    string Evidence
) {
    public RiskSignal(string code, int score, string reason, string evidence)
        : this(code, score, score, reason, evidence) {
    }
}
