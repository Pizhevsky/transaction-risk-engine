using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Tests;

public sealed class TransactionApiIntegrationTests(TransactionRiskEngineApiFactory factory)
    : IClassFixture<TransactionRiskEngineApiFactory>
{
    [PostgreSqlIntegrationFact]
    public async Task Analyse_transaction_returns_explainable_risk_signals() {
        var client = factory.CreateClient();

        var request = new AnalyseTransactionRequest(
            SeedData.AlexId,
            1_000_000,
            "NZD",
            "Online Electronics Store",
            "card-shared-risk-001",
            "4242",
            "device-shared-risk-001",
            "203.0.113.99",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        var response = await client.PostAsJsonAsync("/api/transactions/analyse", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Contains(body!["decision"]!.GetValue<string>(), new[] { "Review", "Blocked" });

        var signals = body["signals"]!.AsArray();
        Assert.Contains(signals, signal => signal!["code"]!.GetValue<string>() == "HIGH_AMOUNT");
        Assert.Contains(signals, signal => signal!["code"]!.GetValue<string>() == "GRAPH_RISK");
    }
}
