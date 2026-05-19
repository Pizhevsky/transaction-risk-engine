using TransactionRiskEngine.Api.Startup;

namespace TransactionRiskEngine.Api.Endpoints;

public static class RuleEndpoints {
    public static IEndpointRouteBuilder MapRuleEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/rules").WithTags("Risk Rules");

        group.MapGet("/", RuleEndpointHandlers.ListAsync);
        group.MapPut("/{code}", RuleEndpointHandlers.UpdateAsync).RequireRateLimiting(ApiPolicyNames.WriteApi);
        group.MapPost("/evaluate", RuleEndpointHandlers.EvaluateAsync).RequireRateLimiting(ApiPolicyNames.WriteApi);
        group.MapGet("/evaluation-jobs", RuleEndpointHandlers.ListJobsAsync);

        return app;
    }
}
