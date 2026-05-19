using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Risk;

namespace TransactionRiskEngine.Tests;

public sealed class AmountAnomalyDetectorTests {
    [Fact]
    public void Detects_high_amount_against_established_history() {
        var history = new[] {
            Transaction(80),
            Transaction(90),
            Transaction(100),
            Transaction(110)
        };

        var signal = AmountAnomalyDetector.Detect(1250, history);

        Assert.NotNull(signal);
        Assert.Equal("HIGH_AMOUNT", signal.Code);
        Assert.Equal(30, signal.Score);
    }

    [Fact]
    public void Scores_extreme_amount_against_history_as_block_severity() {
        var history = new[] {
            Transaction(1200),
            Transaction(1300),
            Transaction(1400),
            Transaction(1500)
        };

        var signal = AmountAnomalyDetector.Detect(1_000_000, history);

        Assert.NotNull(signal);
        Assert.Equal("HIGH_AMOUNT", signal.Code);
        Assert.Equal(85, signal.Score);
    }

    [Fact]
    public void Does_not_flag_amount_exactly_at_three_times_average() {
        var history = new[] {
            Transaction(100),
            Transaction(100),
            Transaction(100)
        };

        var signal = AmountAnomalyDetector.Detect(300, history);

        Assert.Null(signal);
    }

    [Fact]
    public void Detects_amount_above_three_times_average() {
        var history = new[] {
            Transaction(100),
            Transaction(100),
            Transaction(100)
        };

        var signal = AmountAnomalyDetector.Detect(301, history);

        Assert.NotNull(signal);
        Assert.Equal("HIGH_AMOUNT", signal.Code);
        Assert.Equal(30, signal.Score);
    }

    [Fact]
    public void Detects_high_amount_when_history_is_limited() {
        var signal = AmountAnomalyDetector.Detect(1000, Array.Empty<TransactionRecord>());

        Assert.NotNull(signal);
        Assert.Equal("HIGH_AMOUNT", signal.Code);
        Assert.Equal(20, signal.Score);
    }

    [Fact]
    public void Scores_extreme_amount_with_limited_history_as_block_severity() {
        var signal = AmountAnomalyDetector.Detect(1_250_000, Array.Empty<TransactionRecord>());

        Assert.NotNull(signal);
        Assert.Equal("HIGH_AMOUNT", signal.Code);
        Assert.Equal(85, signal.Score);
    }

    [Fact]
    public void Does_not_flag_limited_history_below_absolute_threshold() {
        var signal = AmountAnomalyDetector.Detect(999.99m, new[] { Transaction(120), Transaction(150) });

        Assert.Null(signal);
    }

    [Fact]
    public void Exactly_three_transactions_use_established_history_rule() {
        var history = new[] {
            Transaction(100),
            Transaction(100),
            Transaction(100)
        };

        var signal = AmountAnomalyDetector.Detect(1000, history);

        Assert.NotNull(signal);
        Assert.Equal(30, signal.Score);
    }

    [Fact]
    public void Ignores_normal_amount() {
        var history = new[] {
            Transaction(120),
            Transaction(130),
            Transaction(150)
        };

        var signal = AmountAnomalyDetector.Detect(180, history);

        Assert.Null(signal);
    }

    private static TransactionRecord Transaction(decimal amount) => new() {
        Id = Guid.NewGuid(),
        Amount = amount,
        Successful = true,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
