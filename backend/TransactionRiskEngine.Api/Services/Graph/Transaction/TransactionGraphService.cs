using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Graph;

public sealed class TransactionGraphService(AppDbContext db) : ITransactionGraphService {
    private const int MaxRelatedUsersPerEntity = 12;

    public async Task<GraphResponseDto?> BuildTransactionGraphAsync(
        Guid transactionId,
        CancellationToken cancellationToken
    ) {
        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(x => x.UserProfile)
            .Include(x => x.Device)
            .Include(x => x.PaymentCard)
            .Include(x => x.IpAddressRecord)
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);

        if (transaction is null) {
            return null;
        }

        var graph = new TransactionGraphBuilder(transaction.UserProfileId, transaction.UserProfile.DisplayName);
        graph.AddNode(GraphNodeIds.User(transaction.UserProfileId), transaction.UserProfile.DisplayName, "User", transaction.UserProfile.IsFlagged);

        await AddTransactionDeviceAsync(graph, transaction, cancellationToken);
        await AddTransactionCardAsync(graph, transaction, cancellationToken);
        await AddTransactionIpAsync(graph, transaction, cancellationToken);

        return graph.ToResponse();
    }

    private async Task AddTransactionDeviceAsync(
        TransactionGraphBuilder graph,
        TransactionRecord transaction,
        CancellationToken cancellationToken
    ) {
        if (transaction.Device is null) {
            return;
        }

        var deviceNode = GraphNodeIds.Device(transaction.Device.Id);
        graph.AddEntityNode(deviceNode, transaction.Device.Fingerprint, "Device", transaction.Device.IsFlagged);

        var relatedUsers = await db.UserDevices
            .AsNoTracking()
            .Where(x => x.DeviceId == transaction.Device.Id && x.UserProfileId != transaction.UserProfileId)
            .OrderBy(x => x.UserProfile.DisplayName)
            .Take(MaxRelatedUsersPerEntity)
            .Select(x => new RelatedGraphUser(x.UserProfileId, x.UserProfile.DisplayName, x.UserProfile.IsFlagged))
            .ToListAsync(cancellationToken);

        graph.AddRelatedUsers(deviceNode, transaction.Device.Fingerprint, relatedUsers);
    }

    private async Task AddTransactionCardAsync(
        TransactionGraphBuilder graph,
        TransactionRecord transaction,
        CancellationToken cancellationToken
    ) {
        if (transaction.PaymentCard is null) {
            return;
        }

        var cardNode = GraphNodeIds.Card(transaction.PaymentCard.Id);
        var label = $"{transaction.PaymentCard.Brand} **** {transaction.PaymentCard.Last4}";
        graph.AddEntityNode(cardNode, label, "Card", transaction.PaymentCard.IsFlagged);

        var relatedUsers = await db.UserCards
            .AsNoTracking()
            .Where(x => x.PaymentCardId == transaction.PaymentCard.Id && x.UserProfileId != transaction.UserProfileId)
            .OrderBy(x => x.UserProfile.DisplayName)
            .Take(MaxRelatedUsersPerEntity)
            .Select(x => new RelatedGraphUser(x.UserProfileId, x.UserProfile.DisplayName, x.UserProfile.IsFlagged))
            .ToListAsync(cancellationToken);

        graph.AddRelatedUsers(cardNode, label, relatedUsers);
    }

    private async Task AddTransactionIpAsync(
        TransactionGraphBuilder graph,
        TransactionRecord transaction,
        CancellationToken cancellationToken
    ) {
        if (transaction.IpAddressRecord is null) {
            return;
        }

        var ipNode = GraphNodeIds.Ip(transaction.IpAddressRecord.Id);
        graph.AddEntityNode(ipNode, transaction.IpAddressRecord.Address, "IP", transaction.IpAddressRecord.IsFlagged);

        var relatedUsers = await db.UserIpAddresses
            .AsNoTracking()
            .Where(x => x.IpAddressRecordId == transaction.IpAddressRecord.Id && x.UserProfileId != transaction.UserProfileId)
            .OrderBy(x => x.UserProfile.DisplayName)
            .Take(MaxRelatedUsersPerEntity)
            .Select(x => new RelatedGraphUser(x.UserProfileId, x.UserProfile.DisplayName, x.UserProfile.IsFlagged))
            .ToListAsync(cancellationToken);

        graph.AddRelatedUsers(ipNode, transaction.IpAddressRecord.Address, relatedUsers);
    }

}
