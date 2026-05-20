using System.Net;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Outbox;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class HttpOutboxPublisherTests {
    [Fact]
    public async Task PublishAsync_posts_message_payload_to_configured_endpoint() {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Accepted));
        var publisher = new HttpOutboxPublisher(
            new FixedHttpClientFactory(new HttpClient(handler)),
            Options.Create(new OutboxPublisherOptions { HttpEndpoint = "https://risk-events.example.test/outbox" }));

        var message = new OutboxMessage {
            Id = Guid.NewGuid(),
            Type = "transaction.analysed",
            PayloadJson = "{\"transactionId\":\"abc\"}",
            OccurredAt = DateTimeOffset.UtcNow
        };

        await publisher.PublishAsync(message, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("https://risk-events.example.test/outbox", handler.Request?.RequestUri?.ToString());
        Assert.True(handler.Request?.Headers.Contains("X-Outbox-Message-Id"));
        Assert.Equal(message.Type, handler.Request?.Headers.GetValues("X-Outbox-Type").Single());
        Assert.Contains($"\"id\":\"{message.Id}\"", handler.Body);
        Assert.Contains("\"type\":\"transaction.analysed\"", handler.Body);
        Assert.Contains("\"payload\":{\"transactionId\":\"abc\"}", handler.Body);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
