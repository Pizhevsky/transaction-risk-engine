using TransactionRiskEngine.Api.Domain;
using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Risk;

public sealed record RiskSignalContext(
    UserProfile User,
    AnalyseTransactionRequest Request,
    Device Device,
    PaymentCard Card,
    IpAddressRecord IpAddress,
    bool IsNewDevice,
    DateTimeOffset CreatedAt
);
