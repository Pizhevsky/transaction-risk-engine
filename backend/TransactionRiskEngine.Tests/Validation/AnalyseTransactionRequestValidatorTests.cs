using System.Globalization;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Validation;

namespace TransactionRiskEngine.Tests;

public sealed class AnalyseTransactionRequestValidatorTests {
    [Fact]
    public void Accepts_valid_request() {
        var result = AnalyseTransactionRequestValidator.Validate(ValidRequest());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Rejects_negative_amount_before_database_work() {
        var result = AnalyseTransactionRequestValidator.Validate(ValidRequest(amount: -10));

        Assert.False(result.IsValid);
        Assert.Contains(nameof(AnalyseTransactionRequest.Amount), result.Errors.Keys);
    }

    [Fact]
    public void Amount_validation_is_not_culture_sensitive() {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");

            var result = AnalyseTransactionRequestValidator.Validate(ValidRequest(amount: 0.01m));

            Assert.True(result.IsValid);
        } finally {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("NZDX")]
    [InlineData("12D")]
    public void Rejects_invalid_currency(string currency) {
        var result = AnalyseTransactionRequestValidator.Validate(ValidRequest(currency: currency));

        Assert.False(result.IsValid);
        Assert.Contains(nameof(AnalyseTransactionRequest.Currency), result.Errors.Keys);
    }

    [Fact]
    public void Rejects_empty_user_id() {
        var result = AnalyseTransactionRequestValidator.Validate(ValidRequest(userId: Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(nameof(AnalyseTransactionRequest.UserId), result.Errors.Keys);
    }

    [Fact]
    public void Rejects_invalid_ip_address() {
        var result = AnalyseTransactionRequestValidator.Validate(ValidRequest(ipAddress: "not-an-ip"));

        Assert.False(result.IsValid);
        Assert.Contains(nameof(AnalyseTransactionRequest.IpAddress), result.Errors.Keys);
    }

    private static AnalyseTransactionRequest ValidRequest(
        Guid? userId = null,
        decimal amount = 100,
        string currency = "NZD",
        string ipAddress = "203.0.113.10") =>
        new (
            userId ?? Guid.NewGuid(),
            amount,
            currency,
            "Local Market",
            "card-test-001",
            "4242",
            "device-test-001",
            ipAddress,
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );
}
