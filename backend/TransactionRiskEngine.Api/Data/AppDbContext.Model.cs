using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Data;

public sealed partial class AppDbContext {
    private static void ConfigureModel(ModelBuilder modelBuilder) {
        ConfigureUsers(modelBuilder);
        ConfigurePaymentEntities(modelBuilder);
        ConfigureUserLinks(modelBuilder);
        ConfigureTransactions(modelBuilder);
        ConfigureRulesAndEvaluation(modelBuilder);
        ConfigureOperationalEntities(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder) {
        modelBuilder.Entity<UserProfile>(entity => {
            entity.ToTable("Users");
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(160);
            entity.Property(x => x.Email).HasMaxLength(256);
        });
    }

    private static void ConfigurePaymentEntities(ModelBuilder modelBuilder) {
        modelBuilder.Entity<PaymentCard>(entity => {
            entity.ToTable("PaymentCards");
            entity.HasIndex(x => x.Fingerprint).IsUnique();
            entity.Property(x => x.Fingerprint).HasMaxLength(128);
            entity.Property(x => x.Brand).HasMaxLength(64);
            entity.Property(x => x.Last4).HasMaxLength(4);
        });

        modelBuilder.Entity<Device>(entity => {
            entity.ToTable("Devices");
            entity.HasIndex(x => x.Fingerprint).IsUnique();
            entity.Property(x => x.Fingerprint).HasMaxLength(128);
            entity.Property(x => x.Label).HasMaxLength(160);
        });

        modelBuilder.Entity<IpAddressRecord>(entity => {
            entity.ToTable("IpAddresses");
            entity.HasIndex(x => x.Address).IsUnique();
            entity.Property(x => x.Address).HasMaxLength(64);
        });
    }

    private static void ConfigureUserLinks(ModelBuilder modelBuilder) {
        modelBuilder.Entity<UserDevice>(entity => {
            entity.ToTable("UserDevices");
            entity.HasKey(x => new { x.UserProfileId, x.DeviceId });
        });

        modelBuilder.Entity<UserCard>(entity => {
            entity.ToTable("UserCards");
            entity.HasKey(x => new { x.UserProfileId, x.PaymentCardId });
        });

        modelBuilder.Entity<UserIpAddress>(entity => {
            entity.ToTable("UserIpAddresses");
            entity.HasKey(x => new { x.UserProfileId, x.IpAddressRecordId });
        });
    }

    private static void ConfigureTransactions(ModelBuilder modelBuilder) {
        modelBuilder.Entity<TransactionRecord>(entity => {
            entity.ToTable("Transactions");
            entity.HasIndex(x => new { x.UserProfileId, x.CreatedAt });
            entity.HasIndex(x => x.RiskScore);
            entity.HasIndex(x => x.Decision);
            entity.HasIndex(x => new { x.UserProfileId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
            entity.Property(x => x.IdempotencyKey).HasMaxLength(96);
            entity.Property(x => x.RequestHash).HasMaxLength(64);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Merchant).HasMaxLength(180);
            entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<RiskEvent>(entity => {
            entity.ToTable("RiskEvents");
            entity.HasIndex(x => x.TransactionRecordId);
            entity.Property(x => x.Code).HasMaxLength(64);
            entity.Property(x => x.Reason).HasMaxLength(300);
            entity.Property(x => x.Evidence).HasMaxLength(1000);
            entity.Property(x => x.BaseScore);
            entity.Property(x => x.Score);
        });

        modelBuilder.Entity<FraudCase>(entity => {
            entity.ToTable("FraudCases");
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Summary).HasMaxLength(400);
            entity.Property(x => x.ReviewNote).HasMaxLength(500);
            entity.HasOne(x => x.TransactionRecord)
                .WithOne(x => x.FraudCase)
                .HasForeignKey<FraudCase>(x => x.TransactionRecordId);
        });
    }

    private static void ConfigureRulesAndEvaluation(ModelBuilder modelBuilder) {
        modelBuilder.Entity<RiskRule>(entity => {
            entity.ToTable("RiskRules");
            entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasMaxLength(64);
            entity.Property(x => x.Description).HasMaxLength(300);
        });

        modelBuilder.Entity<RiskEvaluationJob>(entity => {
            entity.ToTable("RiskEvaluationJobs");
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Reason).HasMaxLength(300);
        });
    }

    private static void ConfigureOperationalEntities(ModelBuilder modelBuilder) {
        modelBuilder.Entity<OutboxMessage>(entity => {
            entity.ToTable("OutboxMessages");
            entity.HasIndex(x => new { x.Status, x.AttemptCount, x.NextAttemptAt, x.OccurredAt });
            entity.HasIndex(x => new { x.Status, x.LockedAt });
            entity.Property(x => x.Type).HasMaxLength(160);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.LockedBy).HasMaxLength(96);
            entity.Property(x => x.LastError).HasMaxLength(1000);
        });

        modelBuilder.Entity<AuditLog>(entity => {
            entity.ToTable("AuditLogs");
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
            entity.HasIndex(x => new { x.EntityType, x.EntityCode });
            entity.HasIndex(x => x.TransactionRecordId);
            entity.Property(x => x.Action).HasMaxLength(80);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.EntityCode).HasMaxLength(96);
            entity.Property(x => x.CorrelationId).HasMaxLength(96);
            entity.Property(x => x.Summary).HasMaxLength(500);
            entity.HasOne(x => x.TransactionRecord)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.TransactionRecordId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
