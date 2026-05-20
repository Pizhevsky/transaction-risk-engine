namespace TransactionRiskEngine.Api.Endpoints;

public static class FraudCaseEndpoints {
    public static IEndpointRouteBuilder MapFraudCaseEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/fraud-cases").WithTags("Fraud Cases");

        group.MapGet("/", FraudCaseEndpointHandlers.ListAsync);
        group.MapPatch("/{id:guid}/status", FraudCaseEndpointHandlers.UpdateStatusAsync);

        return app;
    }
}
