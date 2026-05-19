using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Graph;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class TransactionGraphServiceTests {
    [Fact]
    public async Task BuildTransactionGraph_uses_only_selected_transaction_entities() {
        await using var db = CreateDb();
        var selectedUserId = Guid.NewGuid();
        var relatedUserId = Guid.NewGuid();
        var unrelatedUserId = Guid.NewGuid();
        var selectedDeviceId = Guid.NewGuid();
        var unrelatedDeviceId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        db.Users.AddRange(
            new UserProfile { Id = selectedUserId, DisplayName = "Selected User" },
            new UserProfile { Id = relatedUserId, DisplayName = "Related User" },
            new UserProfile { Id = unrelatedUserId, DisplayName = "Unrelated User", IsFlagged = true }
        );
        db.Devices.AddRange(
            new Device { Id = selectedDeviceId, Fingerprint = "device-selected" },
            new Device { Id = unrelatedDeviceId, Fingerprint = "device-unrelated" }
        );
        db.UserDevices.AddRange(
            new UserDevice { UserProfileId = selectedUserId, DeviceId = selectedDeviceId, FirstSeenAt = now, LastSeenAt = now },
            new UserDevice { UserProfileId = relatedUserId, DeviceId = selectedDeviceId, FirstSeenAt = now, LastSeenAt = now },
            new UserDevice { UserProfileId = unrelatedUserId, DeviceId = unrelatedDeviceId, FirstSeenAt = now, LastSeenAt = now }
        );
        db.Transactions.Add(new TransactionRecord {
            Id = transactionId,
            UserProfileId = selectedUserId,
            DeviceId = selectedDeviceId,
            Amount = 1250,
            Currency = "NZD",
            Merchant = "Online Store",
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        var service = new TransactionGraphService(db);

        var graph = await service.BuildTransactionGraphAsync(transactionId, CancellationToken.None);

        Assert.NotNull(graph);
        Assert.Equal(selectedUserId, graph!.UserId);
        Assert.Contains(graph.Nodes, x => x.Label == "Selected User");
        Assert.Contains(graph.Nodes, x => x.Label == "Related User");
        Assert.Contains(graph.Nodes, x => x.Label == "device-selected");
        Assert.DoesNotContain(graph.Nodes, x => x.Label == "Unrelated User");
        Assert.DoesNotContain(graph.Nodes, x => x.Label == "device-unrelated");
    }

    private static AppDbContext CreateDb() {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
