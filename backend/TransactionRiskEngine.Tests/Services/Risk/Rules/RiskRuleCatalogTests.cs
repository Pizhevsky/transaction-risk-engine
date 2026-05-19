using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Risk;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class RiskRuleCatalogTests {
    [Fact]
    public async Task GetRulesAsync_caches_snapshot_until_invalidated() {
        await using var db = CreateDb();
        db.RiskRules.Add(new RiskRule {
            Code = "HIGH_AMOUNT",
            Description = "Initial",
            Weight = 30,
            Enabled = true
        });
        await db.SaveChangesAsync();

        var catalog = CreateCatalog(db);
        var first = await catalog.GetRulesAsync(CancellationToken.None);

        var rule = await db.RiskRules.SingleAsync();
        rule.Weight = 50;
        await db.SaveChangesAsync();

        var cached = await catalog.GetRulesAsync(CancellationToken.None);
        catalog.Invalidate();
        var refreshed = await catalog.GetRulesAsync(CancellationToken.None);

        Assert.Equal(30, first["HIGH_AMOUNT"].Weight);
        Assert.Equal(30, cached["HIGH_AMOUNT"].Weight);
        Assert.Equal(50, refreshed["HIGH_AMOUNT"].Weight);
    }

    private static RiskRuleCatalog CreateCatalog(AppDbContext db) => new(
        db,
        new MemoryCache(new MemoryCacheOptions()),
        Options.Create(new RiskRuleCatalogOptions { CacheSeconds = 60 })
    );

    private static AppDbContext CreateDb() {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
