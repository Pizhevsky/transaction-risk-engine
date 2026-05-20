using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;
using TransactionRiskEngine.Api.Services.Graph;
using TransactionRiskEngine.Api.Services.Risk;

namespace TransactionRiskEngine.Tests;

public sealed class RiskSignalBuilderRuleTests {
    [Fact]
    public async Task Disabled_rule_keeps_detector_output_with_zero_applied_score() {
        await using var db = CreateDb();
        db.RiskRules.AddRange(
            new RiskRule { Code = "HIGH_AMOUNT", Description = "High amount", Weight = 30, Enabled = true },
            new RiskRule { Code = "NEW_DEVICE", Description = "New device", Weight = 15, Enabled = false },
            new RiskRule { Code = "VELOCITY_SPIKE", Description = "Velocity", Weight = 20, Enabled = true },
            new RiskRule { Code = "FAILED_ATTEMPTS", Description = "Failed", Weight = 20, Enabled = true },
            new RiskRule { Code = "GRAPH_RISK", Description = "Graph", Weight = 25, Enabled = true });
        await db.SaveChangesAsync();

        var builder = CreateBuilder(db);
        var user = new UserProfile { Id = Guid.NewGuid(), DisplayName = "Alex" };
        var request = new AnalyseTransactionRequest(
            user.Id,
            100,
            "NZD",
            "Local Market",
            "card-1",
            "4242",
            "device-new",
            "203.0.113.10",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        var signals = await builder.BuildAsync(
            new RiskSignalContext(
                user,
                request,
                new Device { Id = Guid.NewGuid(), Fingerprint = "device-new" },
                new PaymentCard { Id = Guid.NewGuid(), Fingerprint = "card-1", Last4 = "4242" },
                new IpAddressRecord { Id = Guid.NewGuid(), Address = "203.0.113.10" },
                IsNewDevice: true,
                CreatedAt: DateTimeOffset.UtcNow
            ),
            CancellationToken.None
        );

        var newDeviceSignal = Assert.Single(signals, x => x.Code == "NEW_DEVICE");
        Assert.Equal(15, newDeviceSignal.BaseScore);
        Assert.Equal(0, newDeviceSignal.Score);
    }

    [Fact]
    public async Task Rule_weight_is_applied_to_signal_score() {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.RiskRules.AddRange(
            new RiskRule { Code = "HIGH_AMOUNT", Description = "High amount", Weight = 45, Enabled = true },
            new RiskRule { Code = "NEW_DEVICE", Description = "New device", Weight = 15, Enabled = true },
            new RiskRule { Code = "VELOCITY_SPIKE", Description = "Velocity", Weight = 20, Enabled = true },
            new RiskRule { Code = "FAILED_ATTEMPTS", Description = "Failed", Weight = 20, Enabled = true },
            new RiskRule { Code = "GRAPH_RISK", Description = "Graph", Weight = 25, Enabled = true }
        );
        db.Transactions.AddRange(
            History(userId, 100),
            History(userId, 100),
            History(userId, 100)
        );
        await db.SaveChangesAsync();

        var builder = CreateBuilder(db);
        var user = new UserProfile { Id = userId, DisplayName = "Alex" };
        var request = new AnalyseTransactionRequest(
            user.Id,
            500,
            "NZD",
            "Electronics",
            "card-1",
            "4242",
            "device-known",
            "203.0.113.10",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        var signals = await builder.BuildAsync(
            new RiskSignalContext(
                user,
                request,
                new Device { Id = Guid.NewGuid(), Fingerprint = "device-known" },
                new PaymentCard { Id = Guid.NewGuid(), Fingerprint = "card-1", Last4 = "4242" },
                new IpAddressRecord { Id = Guid.NewGuid(), Address = "203.0.113.10" },
                IsNewDevice: false,
                CreatedAt: DateTimeOffset.UtcNow
            ),
            CancellationToken.None
        );

        var amountSignal = Assert.Single(signals, x => x.Code == "HIGH_AMOUNT");
        Assert.Equal(45, amountSignal.Score);
    }

    [Fact]
    public async Task Extreme_amount_keeps_detector_score_above_rule_weight() {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.RiskRules.AddRange(
            new RiskRule { Code = "HIGH_AMOUNT", Description = "High amount", Weight = 30, Enabled = true },
            new RiskRule { Code = "NEW_DEVICE", Description = "New device", Weight = 15, Enabled = true },
            new RiskRule { Code = "VELOCITY_SPIKE", Description = "Velocity", Weight = 20, Enabled = true },
            new RiskRule { Code = "FAILED_ATTEMPTS", Description = "Failed", Weight = 20, Enabled = true },
            new RiskRule { Code = "GRAPH_RISK", Description = "Graph", Weight = 25, Enabled = true }
        );
        await db.SaveChangesAsync();

        var builder = CreateBuilder(db);
        var user = new UserProfile { Id = userId, DisplayName = "Hana" };
        var request = new AnalyseTransactionRequest(
            user.Id,
            1_250_000,
            "NZD",
            "Online Electronics Store",
            "card-shared-risk-001",
            "4242",
            "device-shared-risk-001",
            "203.0.113.99",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        var signals = await builder.BuildAsync(
            new RiskSignalContext(
                user,
                request,
                new Device { Id = Guid.NewGuid(), Fingerprint = "device-shared-risk-001" },
                new PaymentCard { Id = Guid.NewGuid(), Fingerprint = "card-shared-risk-001", Last4 = "4242" },
                new IpAddressRecord { Id = Guid.NewGuid(), Address = "203.0.113.99" },
                IsNewDevice: false,
                CreatedAt: DateTimeOffset.UtcNow
            ),
            CancellationToken.None
        );

        var amountSignal = Assert.Single(signals, x => x.Code == "HIGH_AMOUNT");
        Assert.Equal(85, amountSignal.BaseScore);
        Assert.Equal(85, amountSignal.Score);
    }


    [Fact]
    public async Task Graph_risk_rule_contributes_once_with_multiple_evidence_paths() {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var flaggedDeviceUserId = Guid.NewGuid();
        var flaggedIpUserId = Guid.NewGuid();
        var sharedDeviceId = Guid.NewGuid();
        var sharedIpId = Guid.NewGuid();
        db.RiskRules.AddRange(
            new RiskRule { Code = "HIGH_AMOUNT", Description = "High amount", Weight = 30, Enabled = true },
            new RiskRule { Code = "NEW_DEVICE", Description = "New device", Weight = 15, Enabled = true },
            new RiskRule { Code = "VELOCITY_SPIKE", Description = "Velocity", Weight = 20, Enabled = true },
            new RiskRule { Code = "FAILED_ATTEMPTS", Description = "Failed", Weight = 20, Enabled = true },
            new RiskRule { Code = "GRAPH_RISK", Description = "Graph", Weight = 25, Enabled = true }
        );
        db.Users.AddRange(
            new UserProfile { Id = userId, DisplayName = "Alex" },
            new UserProfile { Id = flaggedDeviceUserId, DisplayName = "flagged-1", IsFlagged = true },
            new UserProfile { Id = flaggedIpUserId, DisplayName = "flagged-2", IsFlagged = true }
        );
        db.Devices.Add(new Device { Id = sharedDeviceId, Fingerprint = "device-shared-risk-001" });
        db.IpAddresses.Add(new IpAddressRecord { Id = sharedIpId, Address = "203.0.113.10" });
        db.UserDevices.AddRange(
            LinkUserDevice(userId, sharedDeviceId),
            LinkUserDevice(flaggedDeviceUserId, sharedDeviceId)
        );
        db.UserIpAddresses.AddRange(
            LinkUserIp(userId, sharedIpId),
            LinkUserIp(flaggedIpUserId, sharedIpId)
        );
        await db.SaveChangesAsync();

        var builder = CreateBuilder(db);
        var user = new UserProfile { Id = userId, DisplayName = "Alex" };
        var request = new AnalyseTransactionRequest(
            user.Id,
            100,
            "NZD",
            "Local Market",
            "card-1",
            "4242",
            "device-known",
            "203.0.113.10",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        var signals = await builder.BuildAsync(
            new RiskSignalContext(
                user,
                request,
                new Device { Id = Guid.NewGuid(), Fingerprint = "device-known", IsFlagged = true },
                new PaymentCard { Id = Guid.NewGuid(), Fingerprint = "card-1", Last4 = "4242" },
                new IpAddressRecord { Id = Guid.NewGuid(), Address = "203.0.113.10" },
                IsNewDevice: false,
                CreatedAt: DateTimeOffset.UtcNow
            ),
            CancellationToken.None
        );

        var graphSignal = Assert.Single(signals, x => x.Code == "GRAPH_RISK");
        Assert.Equal(70, graphSignal.BaseScore);
        Assert.Equal(70, graphSignal.Score);
        Assert.Contains("directly flagged", graphSignal.Evidence);
        Assert.Contains("flagged-1", graphSignal.Evidence);
        Assert.Contains("flagged-2", graphSignal.Evidence);
    }


    [Fact]
    public async Task Amount_history_uses_same_currency_only() {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();

        db.RiskRules.AddRange(
            new RiskRule { Code = "HIGH_AMOUNT", Description = "High amount", Weight = 30, Enabled = true },
            new RiskRule { Code = "NEW_DEVICE", Description = "New device", Weight = 15, Enabled = true },
            new RiskRule { Code = "VELOCITY_SPIKE", Description = "Velocity", Weight = 20, Enabled = true },
            new RiskRule { Code = "FAILED_ATTEMPTS", Description = "Failed", Weight = 20, Enabled = true },
            new RiskRule { Code = "GRAPH_RISK", Description = "Graph", Weight = 25, Enabled = true }
        );
        db.Transactions.AddRange(
            History(userId, 10, "USD"),
            History(userId, 10, "USD"),
            History(userId, 10, "USD")
        );
        await db.SaveChangesAsync();

        var builder = CreateBuilder(db);
        var user = new UserProfile { Id = userId, DisplayName = "Alex" };
        var request = new AnalyseTransactionRequest(
            user.Id,
            900,
            "NZD",
            "Local Market",
            "card-1",
            "4242",
            "device-known",
            "203.0.113.10",
            Successful: true,
            CreatedAt: DateTimeOffset.UtcNow
        );

        var signals = await builder.BuildAsync(
            new RiskSignalContext(
                user,
                request,
                new Device { Id = Guid.NewGuid(), Fingerprint = "device-known" },
                new PaymentCard { Id = Guid.NewGuid(), Fingerprint = "card-1", Last4 = "4242" },
                new IpAddressRecord { Id = Guid.NewGuid(), Address = "203.0.113.10" },
                IsNewDevice: false,
                CreatedAt: DateTimeOffset.UtcNow
            ),
            CancellationToken.None
        );

        Assert.DoesNotContain(signals, x => x.Code == "HIGH_AMOUNT");
    }

    private static RiskRuleCatalog CreateCatalog(AppDbContext db) => new(
        db,
        new MemoryCache(new MemoryCacheOptions()),
        Options.Create(new RiskRuleCatalogOptions { CacheSeconds = 30 })
    );

    private static RiskSignalBuilder CreateBuilder(AppDbContext db) => new(
        db,
        new UserGraphService(
            db,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GraphRiskOptions())
        ),
        CreateCatalog(db)
    );

    private static AppDbContext CreateDb() {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static TransactionRecord History(Guid userId, decimal amount) => History(userId, amount, "NZD");

    private static TransactionRecord History(Guid userId, decimal amount, string currency) => new() {
        Id = Guid.NewGuid(),
        UserProfileId = userId,
        Amount = amount,
        Currency = currency,
        Merchant = "History",
        Successful = true,
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        Decision = TransactionDecision.Approved,
        RiskScore = 0
    };

    private static UserDevice LinkUserDevice(Guid userId, Guid deviceId) => new() {
        UserProfileId = userId,
        DeviceId = deviceId,
        FirstSeenAt = DateTimeOffset.UtcNow.AddDays(-1),
        LastSeenAt = DateTimeOffset.UtcNow
    };

    private static UserIpAddress LinkUserIp(Guid userId, Guid ipId) => new() {
        UserProfileId = userId,
        IpAddressRecordId = ipId,
        FirstSeenAt = DateTimeOffset.UtcNow.AddDays(-1),
        LastSeenAt = DateTimeOffset.UtcNow
    };
}
