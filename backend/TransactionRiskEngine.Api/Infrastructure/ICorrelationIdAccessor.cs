namespace TransactionRiskEngine.Api.Infrastructure;

public interface ICorrelationIdAccessor {
    string? Current { get; }
}
