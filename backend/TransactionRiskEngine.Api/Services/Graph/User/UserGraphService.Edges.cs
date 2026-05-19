using Microsoft.EntityFrameworkCore;

namespace TransactionRiskEngine.Api.Services.Graph;

public sealed partial class UserGraphService {
    private static async Task<HashSet<string>> LoadEdgesForFrontierAsync(
        GraphBuildContext context,
        IReadOnlySet<string> frontier,
        Dictionary<string, HashSet<string>> edges,
        CancellationToken cancellationToken
    ) {
        var neighbours = new HashSet<string>(StringComparer.Ordinal);

        await AddUserOutgoingEdgesAsync(context, frontier, edges, neighbours, cancellationToken);
        await AddDeviceUserEdgesAsync(context, frontier, edges, neighbours, cancellationToken);
        await AddCardUserEdgesAsync(context, frontier, edges, neighbours, cancellationToken);
        await AddIpUserEdgesAsync(context, frontier, edges, neighbours, cancellationToken);

        return neighbours;
    }

    private static async Task AddUserOutgoingEdgesAsync(
        GraphBuildContext context,
        IReadOnlySet<string> frontier,
        Dictionary<string, HashSet<string>> edges,
        HashSet<string> neighbours,
        CancellationToken cancellationToken
    ) {
        var userIds = ParseNodeIds(frontier, GraphNodeIds.UserPrefix);
        if (userIds.Count == 0) {
            return;
        }

        await AddUserDeviceEdgesAsync(context, userIds, edges, neighbours, cancellationToken);
        await AddUserCardEdgesAsync(context, userIds, edges, neighbours, cancellationToken);
        await AddUserIpEdgesAsync(context, userIds, edges, neighbours, cancellationToken);
    }

    private static async Task AddUserDeviceEdgesAsync(
        GraphBuildContext context,
        IReadOnlySet<Guid> userIds,
        Dictionary<string, HashSet<string>> edges,
        HashSet<string> neighbours,
        CancellationToken cancellationToken
    ) {
        var links = await context.Db.UserDevices.AsNoTracking()
            .Where(x => userIds.Contains(x.UserProfileId))
            .OrderBy(x => x.DeviceId)
            .Take(context.MaxEdgesPerExpansion)
            .Select(x => new { x.UserProfileId, x.DeviceId })
            .ToListAsync(cancellationToken);

        foreach (var link in links) {
            AddEdge(edges, GraphNodeIds.User(link.UserProfileId), GraphNodeIds.Device(link.DeviceId));
            neighbours.Add(GraphNodeIds.Device(link.DeviceId));
        }
    }

    private static async Task AddUserCardEdgesAsync(
        GraphBuildContext context,
        IReadOnlySet<Guid> userIds,
        Dictionary<string, HashSet<string>> edges,
        HashSet<string> neighbours,
        CancellationToken cancellationToken
    ) {
        var links = await context.Db.UserCards.AsNoTracking()
            .Where(x => userIds.Contains(x.UserProfileId))
            .OrderBy(x => x.PaymentCardId)
            .Take(context.MaxEdgesPerExpansion)
            .Select(x => new { x.UserProfileId, x.PaymentCardId })
            .ToListAsync(cancellationToken);

        foreach (var link in links) {
            AddEdge(edges, GraphNodeIds.User(link.UserProfileId), GraphNodeIds.Card(link.PaymentCardId));
            neighbours.Add(GraphNodeIds.Card(link.PaymentCardId));
        }
    }

    private static async Task AddUserIpEdgesAsync(
        GraphBuildContext context,
        IReadOnlySet<Guid> userIds,
        Dictionary<string, HashSet<string>> edges,
        HashSet<string> neighbours,
        CancellationToken cancellationToken
    ) {
        var links = await context.Db.UserIpAddresses.AsNoTracking()
            .Where(x => userIds.Contains(x.UserProfileId))
            .OrderBy(x => x.IpAddressRecordId)
            .Take(context.MaxEdgesPerExpansion)
            .Select(x => new { x.UserProfileId, x.IpAddressRecordId })
            .ToListAsync(cancellationToken);

        foreach (var link in links) {
            AddEdge(edges, GraphNodeIds.User(link.UserProfileId), GraphNodeIds.Ip(link.IpAddressRecordId));
            neighbours.Add(GraphNodeIds.Ip(link.IpAddressRecordId));
        }
    }

    private static async Task AddDeviceUserEdgesAsync(
        GraphBuildContext context,
        IReadOnlySet<string> frontier,
        Dictionary<string, HashSet<string>> edges,
        HashSet<string> neighbours,
        CancellationToken cancellationToken
    ) {
        var deviceIds = ParseNodeIds(frontier, GraphNodeIds.DevicePrefix);
        if (deviceIds.Count == 0) {
            return;
        }

        var links = await context.Db.UserDevices.AsNoTracking()
            .Where(x => deviceIds.Contains(x.DeviceId))
            .OrderBy(x => x.UserProfileId)
            .Take(context.MaxEdgesPerExpansion)
            .Select(x => new { x.UserProfileId, x.DeviceId })
            .ToListAsync(cancellationToken);

        foreach (var link in links) {
            AddEdge(edges, GraphNodeIds.Device(link.DeviceId), GraphNodeIds.User(link.UserProfileId));
            neighbours.Add(GraphNodeIds.User(link.UserProfileId));
        }
    }

    private static async Task AddCardUserEdgesAsync(
        GraphBuildContext context,
        IReadOnlySet<string> frontier,
        Dictionary<string, HashSet<string>> edges,
        HashSet<string> neighbours,
        CancellationToken cancellationToken
    ) {
        var cardIds = ParseNodeIds(frontier, GraphNodeIds.CardPrefix);
        if (cardIds.Count == 0) {
            return;
        }

        var links = await context.Db.UserCards.AsNoTracking()
            .Where(x => cardIds.Contains(x.PaymentCardId))
            .OrderBy(x => x.UserProfileId)
            .Take(context.MaxEdgesPerExpansion)
            .Select(x => new { x.UserProfileId, x.PaymentCardId })
            .ToListAsync(cancellationToken);

        foreach (var link in links) {
            AddEdge(edges, GraphNodeIds.Card(link.PaymentCardId), GraphNodeIds.User(link.UserProfileId));
            neighbours.Add(GraphNodeIds.User(link.UserProfileId));
        }
    }

    private static async Task AddIpUserEdgesAsync(
        GraphBuildContext context,
        IReadOnlySet<string> frontier,
        Dictionary<string, HashSet<string>> edges,
        HashSet<string> neighbours,
        CancellationToken cancellationToken
    ) {
        var ipIds = ParseNodeIds(frontier, GraphNodeIds.IpPrefix);
        if (ipIds.Count == 0) {
            return;
        }

        var links = await context.Db.UserIpAddresses.AsNoTracking()
            .Where(x => ipIds.Contains(x.IpAddressRecordId))
            .OrderBy(x => x.UserProfileId)
            .Take(context.MaxEdgesPerExpansion)
            .Select(x => new { x.UserProfileId, x.IpAddressRecordId })
            .ToListAsync(cancellationToken);

        foreach (var link in links) {
            AddEdge(edges, GraphNodeIds.Ip(link.IpAddressRecordId), GraphNodeIds.User(link.UserProfileId));
            neighbours.Add(GraphNodeIds.User(link.UserProfileId));
        }
    }
}
