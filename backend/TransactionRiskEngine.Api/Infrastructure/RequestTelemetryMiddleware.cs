using System.Diagnostics;

namespace TransactionRiskEngine.Api.Infrastructure;

public sealed class RequestTelemetryMiddleware(
    RequestDelegate next,
    ILogger<RequestTelemetryMiddleware> logger
) {
    public async Task InvokeAsync(HttpContext context) {
        var startedAt = Stopwatch.GetTimestamp();

        try {
            await next(context);
        } finally {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
                ?? context.TraceIdentifier;

            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms with correlation {CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                elapsed.TotalMilliseconds,
                correlationId
            );
        }
    }
}
