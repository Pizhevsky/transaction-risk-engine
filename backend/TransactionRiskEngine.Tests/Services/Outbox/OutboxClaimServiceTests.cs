using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Outbox;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class OutboxClaimServiceTests {
    [Fact]
    public async Task ClaimBatch_claims_only_pending_messages_for_the_worker() {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var claimable = Message(OutboxMessageStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(-2));
        var alreadyProcessing = Message(OutboxMessageStatus.Processing, DateTimeOffset.UtcNow.AddMinutes(-1));
        alreadyProcessing.LockedBy = "other-worker";
        alreadyProcessing.LockedAt = DateTimeOffset.UtcNow;

        db.OutboxMessages.AddRange(claimable, alreadyProcessing);
        await db.SaveChangesAsync();

        var service = CreateService(batchSize: 10);
        var claimed = await service.ClaimBatchAsync(db, "worker-a", CancellationToken.None);

        Assert.Single(claimed);
        Assert.Equal(claimable.Id, claimed[0].Id);
        Assert.Equal(OutboxMessageStatus.Processing, claimed[0].Status);
        Assert.Equal("worker-a", claimed[0].LockedBy);

        var untouched = await db.OutboxMessages.FindAsync(alreadyProcessing.Id);
        Assert.Equal("other-worker", untouched!.LockedBy);
    }

    [Fact]
    public async Task ClaimBatch_second_worker_does_not_reclaim_first_worker_messages() {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        db.OutboxMessages.Add(Message(OutboxMessageStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(-5)));
        await db.SaveChangesAsync();

        var service = CreateService(batchSize: 10);
        var first = await service.ClaimBatchAsync(db, "worker-a", CancellationToken.None);
        var second = await service.ClaimBatchAsync(db, "worker-b", CancellationToken.None);

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public async Task RequeueStaleProcessingMessages_releases_expired_locks() {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var stale = Message(OutboxMessageStatus.Processing, DateTimeOffset.UtcNow.AddMinutes(-10));
        stale.LockedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        stale.LockedBy = "dead-worker";

        db.OutboxMessages.Add(stale);
        await db.SaveChangesAsync();

        var service = CreateService(lockTimeoutSeconds: 60);
        var count = await service.RequeueStaleProcessingMessagesAsync(db, CancellationToken.None);

        var reloaded = await db.OutboxMessages.FindAsync(stale.Id);
        Assert.Equal(1, count);
        Assert.Equal(OutboxMessageStatus.Pending, reloaded!.Status);
        Assert.Null(reloaded.LockedAt);
        Assert.Null(reloaded.LockedBy);
        Assert.NotNull(reloaded.NextAttemptAt);
    }


    [Fact]
    public async Task RequeueStaleProcessingMessages_does_not_requeue_fresh_processing_locks() {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        var fresh = Message(OutboxMessageStatus.Processing, DateTimeOffset.UtcNow.AddMinutes(-1));
        fresh.LockedAt = DateTimeOffset.UtcNow;
        fresh.LockedBy = "active-worker";

        db.OutboxMessages.Add(fresh);
        await db.SaveChangesAsync();

        var service = CreateService(lockTimeoutSeconds: 60);
        var count = await service.RequeueStaleProcessingMessagesAsync(db, CancellationToken.None);

        var reloaded = await db.OutboxMessages.FindAsync(fresh.Id);
        Assert.Equal(0, count);
        Assert.Equal(OutboxMessageStatus.Processing, reloaded!.Status);
        Assert.Equal("active-worker", reloaded.LockedBy);
        Assert.NotNull(reloaded.LockedAt);
    }

    private static OutboxClaimService CreateService(int batchSize = 25, int lockTimeoutSeconds = 120) =>
        new(Options.Create(new OutboxDispatcherOptions {
            BatchSize = batchSize,
            MaxAttempts = 5,
            LockTimeoutSeconds = lockTimeoutSeconds
        }));

    private static AppDbContext CreateDb(SqliteConnection connection) {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static OutboxMessage Message(OutboxMessageStatus status, DateTimeOffset occurredAt) => new() {
        Id = Guid.NewGuid(),
        Type = "transaction.analysed",
        PayloadJson = "{}",
        Status = status,
        OccurredAt = occurredAt,
        AttemptCount = 0
    };
}
