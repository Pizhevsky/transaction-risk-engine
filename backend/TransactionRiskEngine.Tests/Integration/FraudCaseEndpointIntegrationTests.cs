using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Tests;

public sealed class FraudCaseEndpointIntegrationTests(TransactionRiskEngineApiFactory factory)
    : IClassFixture<TransactionRiskEngineApiFactory>
{
    [PostgreSqlIntegrationFact]
    public async Task Can_move_fraud_case_through_manual_review_statuses() {
        var client = factory.CreateClient();

        var analyse = await client.PostAsJsonAsync("/api/transactions/analyse", new AnalyseTransactionRequest(
            SeedData.AlexId,
            1250,
            "NZD",
            "Online Electronics Store",
            "card-manual-case-001",
            "4242",
            "device-manual-case-001",
            "203.0.113.99",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        ));

        Assert.Equal(HttpStatusCode.Created, analyse.StatusCode);

        var cases = await client.GetFromJsonAsync<JsonArray>("/api/fraud-cases?status=open&limit=20&offset=0");
        Assert.NotNull(cases);
        var fraudCaseId = cases!
            .Select(x => x!.AsObject())
            .First(x => x["summary"]!.GetValue<string>().Contains("NZD 1", StringComparison.OrdinalIgnoreCase))["id"]!
            .GetValue<Guid>();

        var invalid = await client.PatchAsJsonAsync(
            $"/api/fraud-cases/{fraudCaseId}/status",
            new UpdateFraudCaseStatusRequest("99", "Numeric enum values should not be accepted.")
        );

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var investigating = await client.PatchAsJsonAsync(
            $"/api/fraud-cases/{fraudCaseId}/status",
            new UpdateFraudCaseStatusRequest("Investigating", "Manual review started from integration test.")
        );

        Assert.Equal(HttpStatusCode.OK, investigating.StatusCode);
        var investigatingBody = await investigating.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(investigatingBody);
        Assert.Equal("Investigating", investigatingBody!["status"]!.GetValue<string>());
        Assert.Equal(
            "Manual review started from integration test.",
            investigatingBody["reviewNote"]!.GetValue<string>()
        );

        var closed = await client.PatchAsJsonAsync(
            $"/api/fraud-cases/{fraudCaseId}/status",
            new UpdateFraudCaseStatusRequest("ClosedApproved", "Evidence reviewed and accepted.")
        );

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var closedBody = await closed.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(closedBody);
        Assert.Equal("ClosedApproved", closedBody!["status"]!.GetValue<string>());
        Assert.Equal("Evidence reviewed and accepted.", closedBody["reviewNote"]!.GetValue<string>());
        Assert.NotNull(closedBody["closedAt"]);

        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var statusChangedEvent = await db.OutboxMessages
                .AsNoTracking()
                .Where(x => x.Type == "fraud-case.status-changed")
                .OrderByDescending(x => x.OccurredAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(statusChangedEvent);
            Assert.Contains(fraudCaseId.ToString(), statusChangedEvent!.PayloadJson);
            Assert.Contains("ClosedApproved", statusChangedEvent.PayloadJson);
        }

        var reopened = await client.PatchAsJsonAsync(
            $"/api/fraud-cases/{fraudCaseId}/status",
            new UpdateFraudCaseStatusRequest("Investigating", null)
        );

        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        var reopenedBody = await reopened.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(reopenedBody);
        Assert.Equal("Investigating", reopenedBody!["status"]!.GetValue<string>());
        Assert.Equal("Evidence reviewed and accepted.", reopenedBody["reviewNote"]!.GetValue<string>());
        Assert.Null(reopenedBody["closedAt"]);
    }
}
