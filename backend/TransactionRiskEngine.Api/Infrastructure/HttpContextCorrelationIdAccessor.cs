namespace TransactionRiskEngine.Api.Infrastructure;

public sealed class HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor {
    public string? Current => httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HeaderName] as string;
}
