using System.Text.Json;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class OutboxWriter(AppDbContext db) : IOutboxWriter {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add<TPayload>(string type, TPayload payload, DateTimeOffset occurredAt) {
        db.OutboxMessages.Add(new OutboxMessage {
            Id = Guid.NewGuid(),
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            Status = OutboxMessageStatus.Pending,
            OccurredAt = occurredAt
        });
    }
}
