namespace TransactionRiskEngine.Api.Infrastructure;

public sealed class CorrelationIdMiddleware(RequestDelegate next) {
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context) {
        var correlationId = ResolveCorrelationId(context);

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (context.RequestServices
            .GetRequiredService<ILogger<CorrelationIdMiddleware>>()
            .BeginScope(new Dictionary<string, object> { [HeaderName] = correlationId })) {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context) {
        if (context.Request.Headers.TryGetValue(HeaderName, out var header) &&
            !string.IsNullOrWhiteSpace(header)
        ) {
            var value = header.ToString().Trim();
            return value[..Math.Min(value.Length, 96)];
        }

        return context.TraceIdentifier;
    }
}
