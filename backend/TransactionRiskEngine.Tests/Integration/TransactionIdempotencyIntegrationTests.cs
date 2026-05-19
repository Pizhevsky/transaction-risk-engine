using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Tests;

public sealed class TransactionIdempotencyIntegrationTests(TransactionRiskEngineApiFactory factory)
    : IClassFixture<TransactionRiskEngineApiFactory>
{
    [PostgreSqlIntegrationFact]
    public async Task Analyse_transaction_with_same_idempotency_key_returns_existing_result() {
        var client = factory.CreateClient();
        var idempotencyKey = $"it-{Guid.NewGuid():N}";

        var request = new AnalyseTransactionRequest(
            SeedData.AlexId,
            1250,
            "NZD",
            "Online Electronics Store",
            "card-idempotent-001",
            "4242",
            "device-idempotent-001",
            "203.0.113.101",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        var first = await PostAnalyseAsync(client, request, idempotencyKey);
        var second = await PostAnalyseAsync(client, request, idempotencyKey);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        Assert.False(first.Headers.Contains("X-Idempotent-Replay"));
        Assert.True(second.Headers.TryGetValues("X-Idempotent-Replay", out var replayValues));
        Assert.Contains("true", replayValues);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonObject>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonObject>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(
            firstBody!["transactionId"]!.GetValue<Guid>(),
            secondBody!["transactionId"]!.GetValue<Guid>()
        );
        Assert.Equal(
            firstBody["amount"]!.GetValue<decimal>(),
            secondBody!["amount"]!.GetValue<decimal>()
        );
    }

    [PostgreSqlIntegrationFact]
    public async Task Analyse_transaction_with_same_idempotency_key_and_different_body_returns_conflict() {
        var client = factory.CreateClient();
        var idempotencyKey = $"it-{Guid.NewGuid():N}";

        var request = new AnalyseTransactionRequest(
            SeedData.AlexId,
            1250,
            "NZD",
            "Online Electronics Store",
            "card-idempotent-conflict-001",
            "4242",
            "device-idempotent-conflict-001",
            "203.0.113.102",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        var first = await PostAnalyseAsync(client, request, idempotencyKey);
        var second = await PostAnalyseAsync(client, request with { Amount = 1300 }, idempotencyKey);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private static Task<HttpResponseMessage> PostAnalyseAsync(
        HttpClient client,
        AnalyseTransactionRequest request,
        string idempotencyKey
    ) {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/analyse") {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);

        return client.SendAsync(message);
    }
}
