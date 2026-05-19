using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Risk;

public static class RiskDecisionService {
    public static int NormaliseScore(IEnumerable<RiskSignal> signals) {
        return Math.Clamp(signals.Sum(x => x.Score), 0, 100);
    }

    public static TransactionDecision Decide(int score) {
        return score switch {
            >= 85 => TransactionDecision.Blocked,
            >= 50 => TransactionDecision.Review,
            _ => TransactionDecision.Approved
        };
    }
}
