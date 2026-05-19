using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Domain;
using Xunit;

namespace TransactionRiskEngine.Tests;

public sealed class AuditLogModelTests {
    [Fact]
    public void Audit_log_has_optional_transaction_record_foreign_key() {
        using var db = CreateDb();
        var auditLog = db.Model.FindEntityType(typeof(AuditLog));

        Assert.NotNull(auditLog);

        var foreignKey = Assert.Single(
            auditLog!.GetForeignKeys(),
            x => x.PrincipalEntityType.ClrType == typeof(TransactionRecord)
        );
        var property = Assert.Single(foreignKey.Properties);

        Assert.Equal(nameof(AuditLog.TransactionRecordId), property.Name);
        Assert.False(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.SetNull, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Audit_log_supports_code_identity_for_non_guid_entities() {
        using var db = CreateDb();
        var auditLog = db.Model.FindEntityType(typeof(AuditLog));

        Assert.NotNull(auditLog);
        Assert.NotNull(auditLog!.FindProperty(nameof(AuditLog.EntityCode)));
        Assert.True(auditLog.FindProperty(nameof(AuditLog.EntityId))!.IsNullable);
        Assert.Contains(
            auditLog.GetIndexes(),
            index => index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(AuditLog.EntityType),
                nameof(AuditLog.EntityCode)
            ])
        );
    }

    private static AppDbContext CreateDb() {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
