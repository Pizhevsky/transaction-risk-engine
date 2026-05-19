using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Risk;

namespace TransactionRiskEngine.Tests;

public sealed class RiskDecisionTests {
    [Theory]
    [InlineData(0, TransactionDecision.Approved)]
    [InlineData(49, TransactionDecision.Approved)]
    [InlineData(50, TransactionDecision.Review)]
    [InlineData(84, TransactionDecision.Review)]
    [InlineData(85, TransactionDecision.Blocked)]
    [InlineData(100, TransactionDecision.Blocked)]
    public void Decision_thresholds_are_clear(int score, TransactionDecision expected) {
        var actual = RiskDecisionService.Decide(score);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Score_is_capped_between_zero_and_one_hundred() {
        var signals = new[] {
            new RiskSignal("A", 80, "First", "Evidence"),
            new RiskSignal("B", 50, "Second", "Evidence")
        };

        Assert.Equal(100, RiskDecisionService.NormaliseScore(signals));
    }
}
