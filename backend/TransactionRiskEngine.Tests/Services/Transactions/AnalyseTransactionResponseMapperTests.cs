using System.Globalization;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Transactions;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class AnalyseTransactionResponseMapperTests {
    [Fact]
    public void FromRecord_maps_transaction_fields_and_orders_events_by_score() {
        var user = new UserProfile {
            Id = Guid.NewGuid(),
            DisplayName = "Alex Morgan"
        };
        var record = new TransactionRecord {
            Id = Guid.NewGuid(),
            UserProfileId = user.Id,
            Amount = 1250m,
            Currency = "NZD",
            Merchant = "Online Electronics Store",
            RiskScore = 55,
            Decision = TransactionDecision.Review,
            CreatedAt = DateTimeOffset.Parse("2026-05-18T10:00:00Z", CultureInfo.InvariantCulture)
        };
        var events = new[] {
            new RiskEvent {
                Code = "GRAPH_RISK",
                BaseScore = 25,
                Score = 25,
                Reason = "Connected to risky entities.",
                Evidence = "Device link"
            },
            new RiskEvent {
                Code = "HIGH_AMOUNT",
                BaseScore = 45,
                Score = 45,
                Reason = "High amount.",
                Evidence = "Amount baseline"
            }
        };

        var response = AnalyseTransactionResponseMapper.FromRecord(record, user, events);

        Assert.Equal(record.Id, response.TransactionId);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(user.DisplayName, response.UserName);
        Assert.Equal(record.Amount, response.Amount);
        Assert.Equal(record.Currency, response.Currency);
        Assert.Equal(record.Merchant, response.Merchant);
        Assert.Equal(record.RiskScore, response.RiskScore);
        Assert.Equal(nameof(TransactionDecision.Review), response.Decision);
        Assert.Equal(record.CreatedAt, response.CreatedAt);

        Assert.Collection(
            response.Signals,
            signal => {
                Assert.Equal("HIGH_AMOUNT", signal.Code);
                Assert.Equal(45, signal.BaseScore);
                Assert.Equal(45, signal.Score);
            },
            signal => {
                Assert.Equal("GRAPH_RISK", signal.Code);
                Assert.Equal(25, signal.BaseScore);
                Assert.Equal(25, signal.Score);
            }
        );
    }
}
