using TransactionRiskEngine.Api.Services.Risk;

namespace TransactionRiskEngine.Tests;

public sealed class RiskRuleApplicatorTests {
    [Fact]
    public void Disabled_rule_preserves_signal_with_zero_applied_score() {
        var rules = new Dictionary<string, RiskRuleSnapshot>(StringComparer.OrdinalIgnoreCase) {
            ["NEW_DEVICE"] = new("NEW_DEVICE", "New device", 15, Enabled: false)
        };

        var result = RiskRuleApplicator.Apply(
            new RiskSignal("NEW_DEVICE", 15, "New device", "Evidence"),
            rules
        );

        Assert.NotNull(result);
        Assert.Equal(15, result!.BaseScore);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void Enabled_rule_controls_score() {
        var rules = new Dictionary<string, RiskRuleSnapshot>(StringComparer.OrdinalIgnoreCase) {
            ["HIGH_AMOUNT"] = new("HIGH_AMOUNT", "High amount", 45, Enabled: true)
        };

        var result = RiskRuleApplicator.Apply(
            new RiskSignal("HIGH_AMOUNT", 30, "High amount", "Evidence"),
            rules
        );

        Assert.NotNull(result);
        Assert.Equal(45, result!.Score);
    }

    [Fact]
    public void High_amount_preserves_detector_severity_above_rule_weight() {
        var rules = new Dictionary<string, RiskRuleSnapshot>(StringComparer.OrdinalIgnoreCase) {
            ["HIGH_AMOUNT"] = new("HIGH_AMOUNT", "High amount", 30, Enabled: true)
        };

        var result = RiskRuleApplicator.Apply(
            new RiskSignal("HIGH_AMOUNT", 70, "Extreme amount", "Evidence"),
            rules
        );

        Assert.NotNull(result);
        Assert.Equal(70, result!.Score);
    }

    [Fact]
    public void Graph_risk_preserves_detector_severity_above_rule_weight() {
        var rules = new Dictionary<string, RiskRuleSnapshot>(StringComparer.OrdinalIgnoreCase) {
            ["GRAPH_RISK"] = new("GRAPH_RISK", "Graph risk", 25, Enabled: true)
        };

        var result = RiskRuleApplicator.Apply(
            new RiskSignal("GRAPH_RISK", 70, "Direct graph risk", "Flagged shared entity"),
            rules
        );

        Assert.NotNull(result);
        Assert.Equal(70, result!.Score);
    }

    [Fact]
    public void Failed_attempts_preserves_lower_severity_for_failed_current_transaction() {
        var rules = new Dictionary<string, RiskRuleSnapshot>(StringComparer.OrdinalIgnoreCase) {
            ["FAILED_ATTEMPTS"] = new("FAILED_ATTEMPTS", "Failed attempts", 20, Enabled: true)
        };

        var result = RiskRuleApplicator.Apply(
            new RiskSignal("FAILED_ATTEMPTS", 10, "Failed", "Current transaction failed"),
            rules
        );

        Assert.NotNull(result);
        Assert.Equal(10, result!.Score);
    }
}
