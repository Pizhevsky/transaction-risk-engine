using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Services.Graph;

namespace TransactionRiskEngine.Api.Services.Risk;

public sealed class RiskSignalBuilder(
    AppDbContext db,
    IUserGraphService userGraphService,
    IRiskRuleCatalog riskRuleCatalog
) : IRiskSignalBuilder {
    public async Task<IReadOnlyList<RiskSignal>> BuildAsync(RiskSignalContext context, CancellationToken cancellationToken) {
        var rules = await riskRuleCatalog.GetRulesAsync(cancellationToken);
        var signals = new List<RiskSignal>();

        AddIfPresent(signals, RiskRuleApplicator.Apply(await BuildAmountSignalAsync(context, cancellationToken), rules));
        AddIfPresent(signals, RiskRuleApplicator.Apply(BuildNewDeviceSignal(context), rules));
        AddIfPresent(signals, RiskRuleApplicator.Apply(await BuildVelocitySignalAsync(context, cancellationToken), rules));
        AddIfPresent(signals, RiskRuleApplicator.Apply(await BuildFailedAttemptsSignalAsync(context, cancellationToken), rules));
        signals.AddRange(await BuildGraphSignalsAsync(context, rules, cancellationToken));

        return signals;
    }

    private async Task<RiskSignal?> BuildAmountSignalAsync(RiskSignalContext context, CancellationToken cancellationToken) {
        var history = await db.Transactions
            .AsNoTracking()
            .Where(x => x.UserProfileId == context.User.Id && x.Successful && x.CreatedAt < context.CreatedAt)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return AmountAnomalyDetector.Detect(context.Request.Amount, history);
    }

    private static RiskSignal? BuildNewDeviceSignal(RiskSignalContext context) {
        return context.IsNewDevice
            ? new RiskSignal(
                "NEW_DEVICE",
                15,
                "New device used for this user.",
                $"Device fingerprint {context.Device.Fingerprint} had not been seen for {context.User.DisplayName}.")
            : null;
    }

    private async Task<RiskSignal?> BuildVelocitySignalAsync(RiskSignalContext context, CancellationToken cancellationToken) {
        var tenMinutesAgo = context.CreatedAt.AddMinutes(-10);
        var countLastTenMinutes = await db.Transactions
            .AsNoTracking()
            .CountAsync(
                x => x.UserProfileId == context.User.Id &&
                    x.CreatedAt >= tenMinutesAgo &&
                    x.CreatedAt < context.CreatedAt,
                cancellationToken
            );

        return VelocityRiskSignalFactory.FromRecentTransactionCount(countLastTenMinutes);
    }

    private async Task<RiskSignal?> BuildFailedAttemptsSignalAsync(RiskSignalContext context, CancellationToken cancellationToken) {
        var fifteenMinutesAgo = context.CreatedAt.AddMinutes(-15);
        var failedAttempts = await db.Transactions
            .AsNoTracking()
            .CountAsync(
                x => x.UserProfileId == context.User.Id
                    && !x.Successful
                    && x.CreatedAt >= fifteenMinutesAgo
                    && x.CreatedAt < context.CreatedAt,
                cancellationToken
            );

        return VelocityRiskSignalFactory.FromFailedAttempts(failedAttempts, context.Request.Successful);
    }

    private async Task<IReadOnlyList<RiskSignal>> BuildGraphSignalsAsync(
        RiskSignalContext context,
        IReadOnlyDictionary<string, RiskRuleSnapshot> rules,
        CancellationToken cancellationToken
    ) {
        if (!rules.TryGetValue("GRAPH_RISK", out var graphRule) || !graphRule.Enabled) {
            return [];
        }

        var evidence = new List<string>();

        if (context.Device.IsFlagged || context.Card.IsFlagged || context.IpAddress.IsFlagged) {
            evidence.Add("The device, card, or IP address is directly flagged.");
        }

        var graphPaths = await userGraphService.FindRiskPathsAsync(
            context.User.Id,
            maxDepth: 3,
            cancellationToken
        );
        evidence.AddRange(graphPaths
            .Take(3)
            .Select(path => string.Join(" -> ", path.Nodes))
        );

        if (evidence.Count == 0) {
            return [];
        }

        var score = ScoreGraphRisk(context, graphPaths.Count);
        var rawSignal = new RiskSignal(
            "GRAPH_RISK",
            score,
            evidence.Count == 1
                ? "Connected to a risky entity through relationship graph."
                : "Connected to risky entities through relationship graph.",
            string.Join(" | ", evidence));

        var appliedSignal = RiskRuleApplicator.Apply(rawSignal, rules);
        return appliedSignal is null ? [] : [appliedSignal];
    }

    private static int ScoreGraphRisk(RiskSignalContext context, int pathCount) {
        if (context.Device.IsFlagged || context.Card.IsFlagged || context.IpAddress.IsFlagged) {
            return 70;
        }

        return pathCount switch {
            >= 3 => 60,
            >= 2 => 50,
            _ => 35
        };
    }

    private static void AddIfPresent(ICollection<RiskSignal> signals, RiskSignal? signal) {
        if (signal is not null) {
            signals.Add(signal);
        }
    }
}
