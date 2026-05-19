using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Validation;

namespace TransactionRiskEngine.Tests;

public sealed class RequestValidationSecurityTests {
    [Theory]
    [InlineData("short")]
    [InlineData("bad key with spaces")]
    [InlineData("bad/key/with/slashes")]
    public void Idempotency_key_validation_rejects_unsafe_values(string key) {
        var result = AnalyseTransactionRequestValidator.ValidateIdempotencyKey(key);

        Assert.False(result.IsValid);
        Assert.Contains("IdempotencyKey", result.Errors.Keys);
    }

    [Fact]
    public void Request_body_idempotency_key_is_validated_too() {
        var request = new AnalyseTransactionRequest(
            Guid.NewGuid(),
            100,
            "NZD",
            "Coffee Shop",
            "card-safe-001",
            "4242",
            "device-safe-001",
            "203.0.113.5",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow,
            IdempotencyKey: "bad key with spaces"
        );

        var result = AnalyseTransactionRequestValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("IdempotencyKey", result.Errors.Keys);
    }
}
