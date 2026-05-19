using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Services.Transactions;

namespace TransactionRiskEngine.Api.Startup;

public static class RiskRuleBootstrapper {
    private static readonly RiskRule[] DefaultRules = [
        new() { Code = "HIGH_AMOUNT", Weight = 30, Description = "Transaction amount is much higher than user history.", Enabled = true },
        new() { Code = "NEW_DEVICE", Weight = 15, Description = "User is using a device not seen before.", Enabled = true },
        new() { Code = "VELOCITY_SPIKE", Weight = 20, Description = "Too many transactions in a short time window.", Enabled = true },
        new() { Code = "FAILED_ATTEMPTS", Weight = 20, Description = "Multiple failed attempts occurred recently.", Enabled = true },
        new() { Code = "GRAPH_RISK", Weight = 25, Description = "User is linked to a flagged user or entity through graph traversal.", Enabled = true }
    ];

    public static async Task EnsureDefaultRiskRulesAsync(AppDbContext db, CancellationToken cancellationToken = default) {
        await TryInsertMissingRulesAsync(db, cancellationToken);
    }

    private static async Task TryInsertMissingRulesAsync(AppDbContext db, CancellationToken cancellationToken) {
        var existingCodes = await db.RiskRules
            .AsNoTracking()
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in DefaultRules.Where(rule => !existing.Contains(rule.Code))) {
            db.RiskRules.Add(new RiskRule {
                Code = rule.Code,
                Description = rule.Description,
                Weight = rule.Weight,
                Enabled = rule.Enabled
            });
        }

        try {
            await db.SaveChangesAsync(cancellationToken);
        } catch (DbUpdateException ex) when (UniqueConstraintDetector.IsUniqueConstraintViolation(ex)) {
            db.ChangeTracker.Clear();
        }
    }
}
