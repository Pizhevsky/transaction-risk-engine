namespace TransactionRiskEngine.Api.Services.Risk;

public interface IRiskSignalBuilder {
    Task<IReadOnlyList<RiskSignal>> BuildAsync(RiskSignalContext context, CancellationToken cancellationToken);
}
