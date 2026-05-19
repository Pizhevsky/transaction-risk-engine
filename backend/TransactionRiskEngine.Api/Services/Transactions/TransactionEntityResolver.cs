using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Transactions;

public sealed class TransactionEntityResolver(AppDbContext db) : ITransactionEntityResolver {
    public async Task<ResolvedTransactionEntities> ResolveAndLinkAsync(
        AnalyseTransactionRequest request,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken
    ) {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"User {request.UserId} was not found.");

        var device = await ResolveDeviceAsync(request.DeviceFingerprint, cancellationToken);
        var card = await ResolveCardAsync(request.CardFingerprint, request.CardLast4, cancellationToken);
        var ip = await ResolveIpAsync(request.IpAddress, cancellationToken);

        var isNewDevice = await LinkDeviceAsync(user.Id, device.Id, createdAt, cancellationToken);
        await LinkCardAsync(user.Id, card.Id, createdAt, cancellationToken);
        await LinkIpAsync(user.Id, ip.Id, createdAt, cancellationToken);

        return new ResolvedTransactionEntities(user, device, card, ip, isNewDevice);
    }

    private async Task<Device> ResolveDeviceAsync(string fingerprint, CancellationToken cancellationToken) {
        var normalized = fingerprint.Trim().ToLowerInvariant();
        var device = await db.Devices.FirstOrDefaultAsync(x => x.Fingerprint == normalized, cancellationToken);
        if (device is not null) {
            return device;
        }

        device = new Device {
            Id = Guid.NewGuid(),
            Fingerprint = normalized,
            Label = normalized
        };

        db.Devices.Add(device);
        return device;
    }

    private async Task<PaymentCard> ResolveCardAsync(string fingerprint, string last4, CancellationToken cancellationToken) {
        var normalized = fingerprint.Trim().ToLowerInvariant();
        var card = await db.PaymentCards.FirstOrDefaultAsync(x => x.Fingerprint == normalized, cancellationToken);
        if (card is not null) {
            return card;
        }

        card = new PaymentCard {
            Id = Guid.NewGuid(),
            Fingerprint = normalized,
            Brand = "Unknown",
            Last4 = string.IsNullOrWhiteSpace(last4) ? "0000" : last4[^Math.Min(4, last4.Length)..]
        };

        db.PaymentCards.Add(card);
        return card;
    }

    private async Task<IpAddressRecord> ResolveIpAsync(string address, CancellationToken cancellationToken) {
        var normalized = address.Trim();
        var ip = await db.IpAddresses.FirstOrDefaultAsync(x => x.Address == normalized, cancellationToken);
        if (ip is not null) {
            return ip;
        }

        ip = new IpAddressRecord {
            Id = Guid.NewGuid(),
            Address = normalized
        };

        db.IpAddresses.Add(ip);
        return ip;
    }

    private async Task<bool> LinkDeviceAsync(Guid userId, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken) {
        var existing = await db.UserDevices.FirstOrDefaultAsync(
            x => x.UserProfileId == userId && x.DeviceId == deviceId,
            cancellationToken
        );

        if (existing is not null) {
            existing.LastSeenAt = now;
            return false;
        }

        db.UserDevices.Add(new UserDevice {
            UserProfileId = userId,
            DeviceId = deviceId,
            FirstSeenAt = now,
            LastSeenAt = now
        });

        return true;
    }

    private async Task LinkCardAsync(Guid userId, Guid cardId, DateTimeOffset now, CancellationToken cancellationToken) {
        var existing = await db.UserCards.FirstOrDefaultAsync(
            x => x.UserProfileId == userId && x.PaymentCardId == cardId,
            cancellationToken
        );

        if (existing is not null) {
            existing.LastSeenAt = now;
            return;
        }

        db.UserCards.Add(new UserCard {
            UserProfileId = userId,
            PaymentCardId = cardId,
            FirstSeenAt = now,
            LastSeenAt = now
        });
    }

    private async Task LinkIpAsync(Guid userId, Guid ipId, DateTimeOffset now, CancellationToken cancellationToken) {
        var existing = await db.UserIpAddresses.FirstOrDefaultAsync(
            x => x.UserProfileId == userId && x.IpAddressRecordId == ipId,
            cancellationToken
        );

        if (existing is not null) {
            existing.LastSeenAt = now;
            return;
        }

        db.UserIpAddresses.Add(new UserIpAddress {
            UserProfileId = userId,
            IpAddressRecordId = ipId,
            FirstSeenAt = now,
            LastSeenAt = now
        });
    }
}
