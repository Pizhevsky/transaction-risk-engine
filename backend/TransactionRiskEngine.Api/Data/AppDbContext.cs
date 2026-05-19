using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Data;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) {
    public DbSet<UserProfile> Users => Set<UserProfile>();
    public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();
    public DbSet<PaymentCard> PaymentCards => Set<PaymentCard>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<IpAddressRecord> IpAddresses => Set<IpAddressRecord>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<UserCard> UserCards => Set<UserCard>();
    public DbSet<UserIpAddress> UserIpAddresses => Set<UserIpAddress>();
    public DbSet<RiskEvent> RiskEvents => Set<RiskEvent>();
    public DbSet<FraudCase> FraudCases => Set<FraudCase>();
    public DbSet<RiskRule> RiskRules => Set<RiskRule>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RiskEvaluationJob> RiskEvaluationJobs => Set<RiskEvaluationJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        ConfigureModel(modelBuilder);
    }
}
