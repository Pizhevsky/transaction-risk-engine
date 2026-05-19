using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Outbox;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class OutboxDeliveryServiceTests {
    [Fact]
    public async Task Dispatch_success_marks_message_processed() {
        var message = Message();
        var service = new OutboxDeliveryService(
            new FakePublisher(),
            Options.Create(new OutboxDispatcherOptions { MaxAttempts = 3 })
        );

        await service.DispatchAsync(message, CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Processed, message.Status);
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal(1, message.AttemptCount);
        Assert.Null(message.LastError);
        Assert.Null(message.LockedAt);
        Assert.Null(message.LockedBy);
    }

    [Fact]
    public async Task Dispatch_failure_requeues_until_max_attempts() {
        var message = Message();
        var service = new OutboxDeliveryService(
            new FailingPublisher(),
            Options.Create(new OutboxDispatcherOptions { MaxAttempts = 3 })
        );

        await service.DispatchAsync(message, CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.LastError);
        Assert.NotNull(message.NextAttemptAt);
    }

    [Fact]
    public async Task Dispatch_failure_marks_failed_at_max_attempts() {
        var message = Message();
        message.AttemptCount = 2;
        var service = new OutboxDeliveryService(
            new FailingPublisher(),
            Options.Create(new OutboxDispatcherOptions { MaxAttempts = 3 })
        );

        await service.DispatchAsync(message, CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(3, message.AttemptCount);
        Assert.NotNull(message.LastError);
        Assert.Null(message.NextAttemptAt);
    }

    private static OutboxMessage Message() => new() {
        Id = Guid.NewGuid(),
        Type = "transaction.analysed",
        PayloadJson = "{}",
        Status = OutboxMessageStatus.Processing,
        LockedAt = DateTimeOffset.UtcNow,
        LockedBy = "worker-1",
        OccurredAt = DateTimeOffset.UtcNow
    };

    private sealed class FakePublisher : IOutboxPublisher {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingPublisher : IOutboxPublisher {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("publish failed");
    }
}
