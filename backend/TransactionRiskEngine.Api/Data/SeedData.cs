using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Data;

public static class SeedData {
    public static readonly Guid AlexId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid HanaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SamRiskId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static async Task InitialiseAsync(AppDbContext db) {
        var now = DateTimeOffset.UtcNow;

        if (!await db.Users.AnyAsync()) {
            var users = CreateUsers(now);
            var entities = CreatePaymentEntities();
            var transactions = CreateTransactionHistory(users, entities, now);

            db.Users.AddRange(users.Alex, users.Hana, users.Sam);
            db.Devices.AddRange(entities.AlexDevice, entities.SharedRiskDevice);
            db.PaymentCards.AddRange(entities.AlexCard, entities.SharedRiskCard);
            db.IpAddresses.AddRange(entities.AlexIp, entities.RiskIp);

            AddUserEntityLinks(db, users, entities, now);
            db.Transactions.AddRange(transactions);
            AddFraudCases(db, transactions, now);
            await db.SaveChangesAsync();
            return;
        }

        await EnsureFraudCasesAsync(db, now);
        await db.SaveChangesAsync();
    }

    private static SeedUsers CreateUsers(DateTimeOffset now) {
        var alex = new UserProfile {
            Id = AlexId,
            DisplayName = "Alex Morgan",
            Email = "alex@example.test",
            CreatedAt = now.AddMonths(-8)
        };

        var hana = new UserProfile {
            Id = HanaId,
            DisplayName = "Hana Patel",
            Email = "hana@example.test",
            CreatedAt = now.AddMonths(-10)
        };

        var sam = new UserProfile {
            Id = SamRiskId,
            DisplayName = "Sam Risk",
            Email = "sam-risk@example.test",
            IsFlagged = true,
            CreatedAt = now.AddMonths(-5)
        };

        return new SeedUsers(alex, hana, sam);
    }

    private static SeedPaymentEntities CreatePaymentEntities() {
        var alexDevice = new Device {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
            Fingerprint = "device-alex-known-001",
            Label = "Alex MacBook"
        };

        var sharedRiskDevice = new Device {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
            Fingerprint = "device-shared-risk-001",
            Label = "Shared suspicious Android"
        };

        var alexCard = new PaymentCard {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
            Fingerprint = "card-alex-known-001",
            Brand = "Visa",
            Last4 = "1234"
        };

        var sharedRiskCard = new PaymentCard {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
            Fingerprint = "card-shared-risk-001",
            Brand = "Mastercard",
            Last4 = "4242"
        };

        var alexIp = new IpAddressRecord {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"),
            Address = "198.51.100.10"
        };

        var riskIp = new IpAddressRecord {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2"),
            Address = "203.0.113.99",
            IsFlagged = true
        };

        return new SeedPaymentEntities(
            alexDevice,
            sharedRiskDevice,
            alexCard,
            sharedRiskCard,
            alexIp,
            riskIp
        );
    }

    private static void AddUserEntityLinks(
        AppDbContext db,
        SeedUsers users,
        SeedPaymentEntities entities,
        DateTimeOffset now
    ) {
        db.UserDevices.AddRange(
            new UserDevice { UserProfileId = users.Alex.Id, DeviceId = entities.AlexDevice.Id, FirstSeenAt = now.AddMonths(-6), LastSeenAt = now.AddDays(-1) },
            new UserDevice { UserProfileId = users.Sam.Id, DeviceId = entities.SharedRiskDevice.Id, FirstSeenAt = now.AddMonths(-1), LastSeenAt = now.AddHours(-2) }
        );

        db.UserCards.AddRange(
            new UserCard { UserProfileId = users.Alex.Id, PaymentCardId = entities.AlexCard.Id, FirstSeenAt = now.AddMonths(-6), LastSeenAt = now.AddDays(-1) },
            new UserCard { UserProfileId = users.Sam.Id, PaymentCardId = entities.SharedRiskCard.Id, FirstSeenAt = now.AddMonths(-1), LastSeenAt = now.AddHours(-2) }
        );

        db.UserIpAddresses.AddRange(
            new UserIpAddress { UserProfileId = users.Alex.Id, IpAddressRecordId = entities.AlexIp.Id, FirstSeenAt = now.AddMonths(-6), LastSeenAt = now.AddDays(-1) },
            new UserIpAddress { UserProfileId = users.Sam.Id, IpAddressRecordId = entities.RiskIp.Id, FirstSeenAt = now.AddMonths(-1), LastSeenAt = now.AddHours(-2) }
        );
    }

    private static IReadOnlyList<TransactionRecord> CreateTransactionHistory(
        SeedUsers users,
        SeedPaymentEntities entities,
        DateTimeOffset now
    ) {
        var history = new List<TransactionRecord>();
        for (var i = 0; i < 8; i++) {
            history.Add(new TransactionRecord {
                Id = Guid.NewGuid(),
                UserProfileId = users.Alex.Id,
                DeviceId = entities.AlexDevice.Id,
                PaymentCardId = entities.AlexCard.Id,
                IpAddressRecordId = entities.AlexIp.Id,
                Amount = 55 + i * 7,
                Currency = "NZD",
                Merchant = i % 2 == 0 ? "Local Grocery" : "Fuel Station",
                Successful = true,
                CreatedAt = now.AddDays(-20 + i),
                RiskScore = 5,
                Decision = TransactionDecision.Approved
            });
        }

        history.Add(new TransactionRecord {
            Id = Guid.NewGuid(),
            UserProfileId = users.Sam.Id,
            DeviceId = entities.SharedRiskDevice.Id,
            PaymentCardId = entities.SharedRiskCard.Id,
            IpAddressRecordId = entities.RiskIp.Id,
            Amount = 1800,
            Currency = "NZD",
            Merchant = "Gift Card Marketplace",
            Successful = false,
            CreatedAt = now.AddHours(-2),
            RiskScore = 92,
            Decision = TransactionDecision.Blocked
        });

        return history;
    }

    private static async Task EnsureFraudCasesAsync(AppDbContext db, DateTimeOffset createdAt) {
        var riskTransactions = await db.Transactions
            .Include(x => x.FraudCase)
            .Where(x => x.Decision == TransactionDecision.Review ||
                x.Decision == TransactionDecision.Blocked)
            .ToListAsync();

        AddFraudCases(db, riskTransactions.Where(x => x.FraudCase is null), createdAt);
    }

    private static void AddFraudCases(
        AppDbContext db,
        IEnumerable<TransactionRecord> transactions,
        DateTimeOffset createdAt
    ) {
        foreach (var transaction in transactions.Where(IsManualReviewRequired)) {
            db.FraudCases.Add(new FraudCase {
                Id = Guid.NewGuid(),
                TransactionRecordId = transaction.Id,
                Status = FraudCaseStatus.Open,
                Summary = $"{transaction.Decision} decision for {transaction.Currency} {transaction.Amount:N2} at {transaction.Merchant}",
                CreatedAt = createdAt
            });
        }
    }

    private static bool IsManualReviewRequired(TransactionRecord transaction) =>
        transaction.Decision is TransactionDecision.Review or TransactionDecision.Blocked;

    private sealed record SeedUsers(
        UserProfile Alex,
        UserProfile Hana,
        UserProfile Sam
    );

    private sealed record SeedPaymentEntities(
        Device AlexDevice,
        Device SharedRiskDevice,
        PaymentCard AlexCard,
        PaymentCard SharedRiskCard,
        IpAddressRecord AlexIp,
        IpAddressRecord RiskIp
    );
}
