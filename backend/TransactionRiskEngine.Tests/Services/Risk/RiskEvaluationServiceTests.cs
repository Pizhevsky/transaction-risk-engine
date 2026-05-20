using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Infrastructure;
using TransactionRiskEngine.Api.Services.Risk;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class RiskEvaluationServiceTests {
    [Fact]
    public async Task Evaluation_preserves_base_score_and_only_changes_applied_score() {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = CreateDb(databaseName);
        var userId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        db.Users.Add(new UserProfile {
            Id = userId,
            DisplayName = "Alex",
            Email = "alex@example.test"
        });
        db.RiskRules.Add(new RiskRule {
            Code = "HIGH_AMOUNT",
            Description = "High amount",
            Weight = 10,
            Enabled = true
        });
        db.Transactions.Add(new TransactionRecord {
            Id = transactionId,
            UserProfileId = userId,
            Amount = 1200,
            Currency = "NZD",
            Merchant = "Electronics",
            Successful = true,
            CreatedAt = DateTimeOffset.UtcNow,
            RiskScore = 45,
            Decision = TransactionDecision.Review,
            RiskEvents = [
                new RiskEvent {
                    Id = Guid.NewGuid(),
                    Code = "HIGH_AMOUNT",
                    BaseScore = 30,
                    Score = 45,
                    Reason = "High amount",
                    Evidence = "Original detector evidence"
                }
            ]
        });
        await db.SaveChangesAsync();

        var service = CreateService(databaseName, db);

        var result = await service.EvaluateRecentTransactionsAsync(10, "test", CancellationToken.None);

        db.ChangeTracker.Clear();
        var riskEvent = await db.RiskEvents.SingleAsync();
        var transaction = await db.Transactions.SingleAsync();
        var auditLog = await db.AuditLogs.SingleAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(30, riskEvent.BaseScore);
        Assert.Equal(30, riskEvent.Score);
        Assert.Equal(30, transaction.RiskScore);
        Assert.Equal(TransactionDecision.Approved, transaction.Decision);
        Assert.Equal("TransactionRiskEvaluated", auditLog.Action);
        Assert.Equal(transactionId, auditLog.EntityId);
        Assert.Equal(transactionId, auditLog.TransactionRecordId);
    }


    [Fact]
    public async Task Evaluation_creates_fraud_case_when_decision_moves_to_review() {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = CreateDb(databaseName);
        var userId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        db.Users.Add(new UserProfile {
            Id = userId,
            DisplayName = "Morgan",
            Email = "morgan@example.test"
        });
        db.RiskRules.Add(new RiskRule {
            Code = "HIGH_AMOUNT",
            Description = "High amount",
            Weight = 80,
            Enabled = true
        });
        db.Transactions.Add(new TransactionRecord {
            Id = transactionId,
            UserProfileId = userId,
            Amount = 2200,
            Currency = "NZD",
            Merchant = "Electronics",
            Successful = true,
            CreatedAt = DateTimeOffset.UtcNow,
            RiskScore = 10,
            Decision = TransactionDecision.Approved,
            RiskEvents = [
                new RiskEvent {
                    Id = Guid.NewGuid(),
                    Code = "HIGH_AMOUNT",
                    BaseScore = 30,
                    Score = 10,
                    Reason = "High amount",
                    Evidence = "Original detector evidence"
                }
            ]
        });
        await db.SaveChangesAsync();

        var service = CreateService(databaseName, db);

        var result = await service.EvaluateRecentTransactionsAsync(10, "raise high amount", CancellationToken.None);

        db.ChangeTracker.Clear();
        var transaction = await db.Transactions.SingleAsync();
        var fraudCase = await db.FraudCases.SingleAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(80, transaction.RiskScore);
        Assert.Equal(TransactionDecision.Review, transaction.Decision);
        Assert.Equal(transactionId, fraudCase.TransactionRecordId);
        Assert.Equal(FraudCaseStatus.Open, fraudCase.Status);
    }

    [Fact]
    public async Task Evaluation_repairs_stored_high_amount_base_score_for_extreme_amounts() {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = CreateDb(databaseName);
        var userId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        db.Users.Add(new UserProfile {
            Id = userId,
            DisplayName = "Hana",
            Email = "hana@example.test"
        });
        db.RiskRules.Add(new RiskRule {
            Code = "HIGH_AMOUNT",
            Description = "High amount",
            Weight = 30,
            Enabled = true
        });
        db.Transactions.Add(new TransactionRecord {
            Id = transactionId,
            UserProfileId = userId,
            Amount = 1_250_000,
            Currency = "NZD",
            Merchant = "Online Electronics Store",
            Successful = true,
            CreatedAt = DateTimeOffset.UtcNow,
            RiskScore = 30,
            Decision = TransactionDecision.Approved,
            RiskEvents = [
                new RiskEvent {
                    Id = Guid.NewGuid(),
                    Code = "HIGH_AMOUNT",
                    BaseScore = 20,
                    Score = 30,
                    Reason = "High amount with limited history.",
                    Evidence = "Old limited-history evidence"
                }
            ]
        });
        await db.SaveChangesAsync();

        var service = CreateService(databaseName, db);

        var result = await service.EvaluateRecentTransactionsAsync(10, "repair amount severity", CancellationToken.None);

        db.ChangeTracker.Clear();
        var riskEvent = await db.RiskEvents.SingleAsync();
        var transaction = await db.Transactions.SingleAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(85, riskEvent.BaseScore);
        Assert.Equal(85, riskEvent.Score);
        Assert.Equal(85, transaction.RiskScore);
        Assert.Equal(TransactionDecision.Blocked, transaction.Decision);
    }


    [Fact]
    public async Task Evaluation_closes_open_fraud_case_when_decision_moves_to_approved() {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = CreateDb(databaseName);
        var userId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        db.Users.Add(new UserProfile {
            Id = userId,
            DisplayName = "Riley",
            Email = "riley@example.test"
        });
        db.RiskRules.Add(new RiskRule {
            Code = "HIGH_AMOUNT",
            Description = "High amount",
            Weight = 10,
            Enabled = true
        });
        db.Transactions.Add(new TransactionRecord {
            Id = transactionId,
            UserProfileId = userId,
            Amount = 1200,
            Currency = "NZD",
            Merchant = "Electronics",
            Successful = true,
            CreatedAt = DateTimeOffset.UtcNow,
            RiskScore = 70,
            Decision = TransactionDecision.Review,
            RiskEvents = [
                new RiskEvent {
                    Id = Guid.NewGuid(),
                    Code = "HIGH_AMOUNT",
                    BaseScore = 30,
                    Score = 70,
                    Reason = "High amount",
                    Evidence = "Original detector evidence"
                }
            ],
            FraudCase = new FraudCase {
                Id = Guid.NewGuid(),
                Status = FraudCaseStatus.Open,
                Summary = "Original open case",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
        });
        await db.SaveChangesAsync();

        var service = CreateService(databaseName, db);

        var result = await service.EvaluateRecentTransactionsAsync(10, "lower high amount", CancellationToken.None);

        db.ChangeTracker.Clear();
        var transaction = await db.Transactions.Include(x => x.FraudCase).SingleAsync();

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(TransactionDecision.Approved, transaction.Decision);
        Assert.Equal(FraudCaseStatus.ClosedApproved, transaction.FraudCase!.Status);
        Assert.NotNull(transaction.FraudCase.ClosedAt);
    }

    private static RiskRuleCatalog CreateCatalog(AppDbContext db) => new(
        db,
        new MemoryCache(new MemoryCacheOptions()),
        Options.Create(new RiskRuleCatalogOptions { CacheSeconds = 30 })
    );

    private static RiskEvaluationService CreateService(string databaseName, AppDbContext db) => new(
        new TestDbContextFactory(databaseName),
        CreateCatalog(db),
        new RiskEvaluationRecordProcessor(new FixedCorrelationIdAccessor())
    );

    private static AppDbContext CreateDb(string databaseName) {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestDbContextFactory(string databaseName) : IDbContextFactory<AppDbContext> {
        public AppDbContext CreateDbContext() => CreateDb(databaseName);
    }

    private sealed class FixedCorrelationIdAccessor : ICorrelationIdAccessor {
        public string? Current => "test-correlation";
    }
}
