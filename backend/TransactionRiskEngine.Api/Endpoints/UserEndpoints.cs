namespace TransactionRiskEngine.Api.Endpoints;

public static class UserEndpoints {
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/", UserEndpointHandlers.ListAsync);
        group.MapGet("/{id:guid}/risk-profile", UserEndpointHandlers.GetRiskProfileAsync);
        group.MapGet("/{id:guid}/connections", UserEndpointHandlers.GetConnectionsAsync);

        return app;
    }
}
