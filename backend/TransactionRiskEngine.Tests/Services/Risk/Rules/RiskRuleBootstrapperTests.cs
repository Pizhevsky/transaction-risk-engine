using Xunit;
using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Startup;

namespace TransactionRiskEngine.Tests;

public sealed class RiskRuleBootstrapperTests {
    [Fact]
    public async Task Default_rules_are_created_even_without_demo_seed_data() {
        await using var db = CreateDb();

        await RiskRuleBootstrapper.EnsureDefaultRiskRulesAsync(db);

        var codes = await db.RiskRules
            .AsNoTracking()
            .Select(x => x.Code)
            .ToListAsync();

        Assert.Contains("HIGH_AMOUNT", codes);
        Assert.Contains("NEW_DEVICE", codes);
        Assert.Contains("VELOCITY_SPIKE", codes);
        Assert.Contains("FAILED_ATTEMPTS", codes);
        Assert.Contains("GRAPH_RISK", codes);
    }

    [Fact]
    public async Task Bootstrapper_does_not_overwrite_existing_rule_configuration() {
        await using var db = CreateDb();
        await RiskRuleBootstrapper.EnsureDefaultRiskRulesAsync(db);

        var highAmount = await db.RiskRules.SingleAsync(x => x.Code == "HIGH_AMOUNT");
        highAmount.Weight = 12;
        highAmount.Enabled = false;
        await db.SaveChangesAsync();

        await RiskRuleBootstrapper.EnsureDefaultRiskRulesAsync(db);

        var reloaded = await db.RiskRules.AsNoTracking().SingleAsync(x => x.Code == "HIGH_AMOUNT");
        Assert.Equal(12, reloaded.Weight);
        Assert.False(reloaded.Enabled);
    }

    private static AppDbContext CreateDb() {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
