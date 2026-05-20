using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Tests;

public sealed class SeedDataTests {
    [Fact]
    public async Task Initial_seed_creates_fraud_case_for_seeded_blocked_transaction() {
        await using var db = CreateDb();

        await SeedData.InitialiseAsync(db);

        var blockedTransaction = await db.Transactions
            .SingleAsync(x => x.Decision == TransactionDecision.Blocked);
        var fraudCase = await db.FraudCases.SingleAsync();

        Assert.Equal(blockedTransaction.Id, fraudCase.TransactionRecordId);
        Assert.Equal(FraudCaseStatus.Open, fraudCase.Status);
        Assert.Contains("Blocked decision", fraudCase.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seed_repair_does_not_duplicate_existing_fraud_cases() {
        await using var db = CreateDb();

        await SeedData.InitialiseAsync(db);
        await SeedData.InitialiseAsync(db);

        Assert.Equal(1, await db.FraudCases.CountAsync());
    }

    private static AppDbContext CreateDb() {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
