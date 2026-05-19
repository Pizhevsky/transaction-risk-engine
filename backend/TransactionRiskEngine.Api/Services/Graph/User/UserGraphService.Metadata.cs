using Microsoft.EntityFrameworkCore;

namespace TransactionRiskEngine.Api.Services.Graph;

public sealed partial class UserGraphService {
    private static async Task LoadNodeMetadataAsync(
        GraphBuildContext context,
        IReadOnlySet<string> nodeIds,
        Dictionary<string, GraphNodeInfo> nodes,
        CancellationToken cancellationToken
    ) {
        await LoadUserNodesAsync(context, nodeIds, nodes, cancellationToken);
        await LoadDeviceNodesAsync(context, nodeIds, nodes, cancellationToken);
        await LoadCardNodesAsync(context, nodeIds, nodes, cancellationToken);
        await LoadIpNodesAsync(context, nodeIds, nodes, cancellationToken);
    }

    private static async Task LoadUserNodesAsync(
        GraphBuildContext context,
        IReadOnlySet<string> nodeIds,
        Dictionary<string, GraphNodeInfo> nodes,
        CancellationToken cancellationToken
    ) {
        var userIds = ParseNodeIds(nodeIds, GraphNodeIds.UserPrefix);
        if (userIds.Count == 0) {
            return;
        }

        var users = await context.Db.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName, x.IsFlagged })
            .ToListAsync(cancellationToken);

        foreach (var user in users) {
            var nodeId = GraphNodeIds.User(user.Id);
            nodes[nodeId] = new GraphNodeInfo(nodeId, user.DisplayName, "User", user.IsFlagged);
        }
    }

    private static async Task LoadDeviceNodesAsync(
        GraphBuildContext context,
        IReadOnlySet<string> nodeIds,
        Dictionary<string, GraphNodeInfo> nodes,
        CancellationToken cancellationToken
    ) {
        var deviceIds = ParseNodeIds(nodeIds, GraphNodeIds.DevicePrefix);
        if (deviceIds.Count == 0) {
            return;
        }

        var devices = await context.Db.Devices
            .AsNoTracking()
            .Where(x => deviceIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Fingerprint, x.IsFlagged })
            .ToListAsync(cancellationToken);

        foreach (var device in devices) {
            var nodeId = GraphNodeIds.Device(device.Id);
            nodes[nodeId] = new GraphNodeInfo(nodeId, device.Fingerprint, "Device", device.IsFlagged);
        }
    }

    private static async Task LoadCardNodesAsync(
        GraphBuildContext context,
        IReadOnlySet<string> nodeIds,
        Dictionary<string, GraphNodeInfo> nodes,
        CancellationToken cancellationToken
    ) {
        var cardIds = ParseNodeIds(nodeIds, GraphNodeIds.CardPrefix);
        if (cardIds.Count == 0) {
            return;
        }

        var cards = await context.Db.PaymentCards
            .AsNoTracking()
            .Where(x => cardIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Brand, x.Last4, x.IsFlagged })
            .ToListAsync(cancellationToken);

        foreach (var card in cards) {
            var nodeId = GraphNodeIds.Card(card.Id);
            nodes[nodeId] = new GraphNodeInfo(nodeId, $"{card.Brand} **** {card.Last4}", "Card", card.IsFlagged);
        }
    }

    private static async Task LoadIpNodesAsync(
        GraphBuildContext context,
        IReadOnlySet<string> nodeIds,
        Dictionary<string, GraphNodeInfo> nodes,
        CancellationToken cancellationToken
    ) {
        var ipIds = ParseNodeIds(nodeIds, GraphNodeIds.IpPrefix);
        if (ipIds.Count == 0) {
            return;
        }

        var ips = await context.Db.IpAddresses
            .AsNoTracking()
            .Where(x => ipIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Address, x.IsFlagged })
            .ToListAsync(cancellationToken);

        foreach (var ip in ips) {
            var nodeId = GraphNodeIds.Ip(ip.Id);
            nodes[nodeId] = new GraphNodeInfo(nodeId, ip.Address, "IP", ip.IsFlagged);
        }
    }
}
