using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class HttpOutboxPublisher(
    IHttpClientFactory httpClientFactory,
    IOptions<OutboxPublisherOptions> options
) : IOutboxPublisher {
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) {
        var endpoint = options.Value.HttpEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint)) {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
            Content = new StringContent(BuildEnvelope(message), Encoding.UTF8, "application/json")
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Outbox-Message-Id", message.Id.ToString());
        request.Headers.TryAddWithoutValidation("X-Outbox-Type", message.Type);
        request.Headers.TryAddWithoutValidation("X-Outbox-Occurred-At", message.OccurredAt.ToString("O"));

        using var response = await httpClientFactory
            .CreateClient("outbox-publisher")
            .SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static string BuildEnvelope(OutboxMessage message) {
        using var payload = JsonDocument.Parse(message.PayloadJson);
        return JsonSerializer.Serialize(new {
            id = message.Id,
            type = message.Type,
            occurredAt = message.OccurredAt,
            payload = payload.RootElement.Clone()
        });
    }
}
