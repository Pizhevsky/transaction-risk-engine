using TransactionRiskEngine.Api.Startup;

namespace TransactionRiskEngine.Api.Endpoints;

public static class TransactionEndpoints {
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/transactions").WithTags("Transactions");

        group.MapGet("/", TransactionEndpointHandlers.ListAsync);
        group.MapGet("/{id:guid}", TransactionEndpointHandlers.GetDetailAsync);
        group.MapGet("/{id:guid}/connections", TransactionEndpointHandlers.GetConnectionsAsync);
        group.MapPost("/analyse", TransactionEndpointHandlers.AnalyseAsync)
            .RequireRateLimiting(ApiPolicyNames.WriteApi);

        return app;
    }
}
