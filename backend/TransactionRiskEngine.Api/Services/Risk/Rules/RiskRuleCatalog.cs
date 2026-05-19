using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;

namespace TransactionRiskEngine.Api.Services.Risk;

public sealed class RiskRuleCatalog(
    AppDbContext db,
    IMemoryCache cache,
    IOptions<RiskRuleCatalogOptions> options
) : IRiskRuleCatalog {
    private const string CacheKey = "risk-rules:v1";

    public async Task<IReadOnlyDictionary<string, RiskRuleSnapshot>> GetRulesAsync(CancellationToken cancellationToken) {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, RiskRuleSnapshot>? cached) && cached is not null) {
            return cached;
        }

        var rules = await db.RiskRules
            .AsNoTracking()
            .Select(x => new RiskRuleSnapshot(x.Code, x.Description, x.Weight, x.Enabled))
            .ToListAsync(cancellationToken);

        var snapshot = rules.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        cache.Set(
            CacheKey,
            snapshot,
            TimeSpan.FromSeconds(Math.Clamp(options.Value.CacheSeconds, 1, 300))
        );

        return snapshot;
    }

    public void Invalidate() {
        cache.Remove(CacheKey);
    }
}
