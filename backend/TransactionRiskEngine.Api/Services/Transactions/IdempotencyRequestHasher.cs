using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Transactions;

public static class IdempotencyRequestHasher {
    public static string Compute(AnalyseTransactionRequest request) {
        var canonicalPayload = new {
            userId = request.UserId,
            amount = decimal.Round(request.Amount, 2),
            currency = request.Currency.Trim().ToUpperInvariant(),
            merchant = request.Merchant.Trim(),
            cardFingerprint = request.CardFingerprint.Trim(),
            cardLast4 = request.CardLast4.Trim(),
            deviceFingerprint = request.DeviceFingerprint.Trim(),
            ipAddress = request.IpAddress.Trim(),
            successful = request.Successful,
            createdAt = request.CreatedAt
        };

        var json = JsonSerializer.Serialize(canonicalPayload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
