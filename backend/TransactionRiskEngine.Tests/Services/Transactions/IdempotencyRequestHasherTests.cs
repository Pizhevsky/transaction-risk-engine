using System.Globalization;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Services.Transactions;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class IdempotencyRequestHasherTests {
    [Fact]
    public void Compute_normalizes_stable_request_fields() {
        var createdAt = DateTimeOffset.Parse("2026-05-18T10:00:00Z", CultureInfo.InvariantCulture);
        var first = CreateRequest(
            amount: 1250.004m,
            currency: " nzd ",
            merchant: " Online Electronics Store ",
            cardFingerprint: " card-shared-risk-001 ",
            cardLast4: " 4242 ",
            deviceFingerprint: " device-shared-risk-001 ",
            ipAddress: " 203.0.113.99 ",
            createdAt: createdAt
        );
        var second = CreateRequest(
            amount: 1250.00m,
            currency: "NZD",
            merchant: "Online Electronics Store",
            cardFingerprint: "card-shared-risk-001",
            cardLast4: "4242",
            deviceFingerprint: "device-shared-risk-001",
            ipAddress: "203.0.113.99",
            createdAt: createdAt
        );

        Assert.Equal(
            IdempotencyRequestHasher.Compute(first),
            IdempotencyRequestHasher.Compute(second)
        );
    }

    [Fact]
    public void Compute_changes_when_payload_changes() {
        var first = CreateRequest(amount: 1250m);
        var second = CreateRequest(amount: 1251m);

        Assert.NotEqual(
            IdempotencyRequestHasher.Compute(first),
            IdempotencyRequestHasher.Compute(second)
        );
    }

    private static AnalyseTransactionRequest CreateRequest(
        decimal amount,
        string currency = "NZD",
        string merchant = "Online Electronics Store",
        string cardFingerprint = "card-shared-risk-001",
        string cardLast4 = "4242",
        string deviceFingerprint = "device-shared-risk-001",
        string ipAddress = "203.0.113.99",
        DateTimeOffset? createdAt = null
    ) {
        return new AnalyseTransactionRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            amount,
            currency,
            merchant,
            cardFingerprint,
            cardLast4,
            deviceFingerprint,
            ipAddress,
            Successful: true,
            createdAt
        );
    }
}
