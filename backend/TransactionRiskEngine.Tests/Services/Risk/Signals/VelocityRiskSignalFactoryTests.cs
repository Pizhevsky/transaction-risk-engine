using Xunit;
using TransactionRiskEngine.Api.Services.Risk;

namespace TransactionRiskEngine.Tests;

public sealed class VelocityRiskSignalFactoryTests {
    [Theory]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Flags_transaction_velocity_from_threshold(int count, bool expectedFlag) {
        var signal = VelocityRiskSignalFactory.FromRecentTransactionCount(count);

        Assert.Equal(expectedFlag, signal is not null);
        if (expectedFlag) {
            Assert.Equal("VELOCITY_SPIKE", signal!.Code);
            Assert.Equal(20, signal.Score);
            Assert.Contains(count.ToString(), signal.Evidence, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(2, true, true)]
    [InlineData(0, false, false)]
    [InlineData(1, false, true)]
    public void Flags_failed_attempt_patterns(int failedAttempts, bool successful, bool expectedFlag) {
        var signal = VelocityRiskSignalFactory.FromFailedAttempts(failedAttempts, successful);

        Assert.Equal(expectedFlag, signal is not null);
        if (expectedFlag) {
            Assert.Equal("FAILED_ATTEMPTS", signal!.Code);
            Assert.True(signal.Score > 0);
        }
    }
}
