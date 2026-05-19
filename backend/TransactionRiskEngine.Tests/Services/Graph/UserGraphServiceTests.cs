using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Graph;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class UserGraphServiceTests {
    [Fact]
    public async Task BuildGraph_loads_bounded_neighbourhood_not_unrelated_entities() {
        await using var db = CreateDb();
        var source = Guid.NewGuid();
        var linked = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        var sharedDevice = Guid.NewGuid();

        db.Users.AddRange(
            new UserProfile { Id = source, DisplayName = "Source" },
            new UserProfile { Id = linked, DisplayName = "Linked", IsFlagged = true },
            new UserProfile { Id = unrelated, DisplayName = "Unrelated", IsFlagged = true }
        );
        db.Devices.Add(new Device { Id = sharedDevice, Fingerprint = "device-shared" });
        db.UserDevices.AddRange(
            new UserDevice { UserProfileId = source, DeviceId = sharedDevice, FirstSeenAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow },
            new UserDevice { UserProfileId = linked, DeviceId = sharedDevice, FirstSeenAt = DateTimeOffset.UtcNow, LastSeenAt = DateTimeOffset.UtcNow }
        );
        await db.SaveChangesAsync();

        var service = new UserGraphService(
            db,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GraphRiskOptions { MaxDepth = 3, MaxPaths = 3, CacheSeconds = 5 })
        );

        var graph = await service.BuildGraphAsync(source, CancellationToken.None);

        Assert.Contains(graph.Nodes, x => x.Label == "Source");
        Assert.Contains(graph.Nodes, x => x.Label == "Linked" && x.IsRisky);
        Assert.DoesNotContain(graph.Nodes, x => x.Label == "Unrelated");
    }

    [Fact]
    public async Task BuildGraph_respects_max_edges_per_expansion_for_high_degree_nodes() {
        await using var db = CreateDb();
        var source = Guid.NewGuid();
        var sharedIp = Guid.NewGuid();

        db.Users.Add(new UserProfile { Id = source, DisplayName = "Source" });
        db.IpAddresses.Add(new IpAddressRecord { Id = sharedIp, Address = "203.0.113.250" });
        db.UserIpAddresses.Add(new UserIpAddress {
            UserProfileId = source,
            IpAddressRecordId = sharedIp,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        });

        for (var i = 0; i < 20; i++) {
            var userId = Guid.NewGuid();
            db.Users.Add(new UserProfile { Id = userId, DisplayName = $"Linked {i:D2}", IsFlagged = i == 0 });
            db.UserIpAddresses.Add(new UserIpAddress {
                UserProfileId = userId,
                IpAddressRecordId = sharedIp,
                FirstSeenAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var service = new UserGraphService(
            db,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GraphRiskOptions {
                MaxDepth = 3,
                MaxPaths = 3,
                CacheSeconds = 5,
                MaxEdgesPerExpansion = 5,
                MaxTotalNodes = 50
            })
        );

        var graph = await service.BuildGraphAsync(source, CancellationToken.None);

        Assert.True(graph.Nodes.Count <= 7);
    }

    private static AppDbContext CreateDb() {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
