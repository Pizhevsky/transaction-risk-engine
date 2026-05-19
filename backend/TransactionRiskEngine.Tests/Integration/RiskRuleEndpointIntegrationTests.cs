using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace TransactionRiskEngine.Tests;

public sealed class RiskRuleEndpointIntegrationTests(TransactionRiskEngineApiFactory factory)
    : IClassFixture<TransactionRiskEngineApiFactory>
{
    [PostgreSqlIntegrationFact]
    public async Task Can_update_rule_and_trigger_evaluation() {
        var client = factory.CreateClient();

        var update = await client.PutAsJsonAsync(
            "/api/rules/HIGH_AMOUNT",
            new UpdateRiskRuleRequest("High amount rule adjusted in integration test.", 35, Enabled: true)
        );

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(updated);
        Assert.Equal(35, updated!["weight"]!.GetValue<int>());

        var evaluate = await client.PostAsJsonAsync(
            "/api/rules/evaluate",
            new RiskEvaluationRequest(BatchSize: 25, Reason: "Integration test rule evaluation")
        );

        Assert.Equal(HttpStatusCode.Accepted, evaluate.StatusCode);
        var body = await evaluate.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.True(body!["processedCount"]!.GetValue<int>() >= 0);
    }
}
