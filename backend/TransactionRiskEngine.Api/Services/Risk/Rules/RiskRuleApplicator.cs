namespace TransactionRiskEngine.Api.Services.Risk;

public static class RiskRuleApplicator {
    public static RiskSignal? Apply(
        RiskSignal? signal,
        IReadOnlyDictionary<string, RiskRuleSnapshot> rules
    ) {
        if (signal is null) {
            return null;
        }

        var appliedScore = ResolveAppliedScore(signal.Code, signal.BaseScore, rules);
        return appliedScore == 0
            ? null
            : signal with { Score = appliedScore };
    }

    public static int ResolveAppliedScore(
        string code,
        int baseScore,
        IReadOnlyDictionary<string, RiskRuleSnapshot> rules
    ) {
        if (!rules.TryGetValue(code, out var rule) || !rule.Enabled) {
            return 0;
        }

        return ApplyRuleWeight(code, baseScore, rule);
    }

    private static int ApplyRuleWeight(string code, int baseScore, RiskRuleSnapshot rule) {
        if (code.Equals("HIGH_AMOUNT", StringComparison.OrdinalIgnoreCase) ||
            code.Equals("GRAPH_RISK", StringComparison.OrdinalIgnoreCase)) {
            return Math.Max(baseScore, rule.Weight);
        }

        if (code.Equals("FAILED_ATTEMPTS", StringComparison.OrdinalIgnoreCase) &&
            baseScore > 0 &&
            baseScore < rule.Weight) {
            return baseScore;
        }

        return rule.Weight;
    }
}
