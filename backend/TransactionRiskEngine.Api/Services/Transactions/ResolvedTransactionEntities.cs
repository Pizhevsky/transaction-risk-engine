using TransactionRiskEngine.Api.Domain;

namespace TransactionRiskEngine.Api.Services.Transactions;

public sealed record ResolvedTransactionEntities(
    UserProfile User,
    Device Device,
    PaymentCard Card,
    IpAddressRecord IpAddress,
    bool IsNewDevice
);
