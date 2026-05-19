namespace TransactionRiskEngine.Api.Startup;

public static class ApiHeaderNames {
    public const string TotalCount = "X-Total-Count";
    public const string Limit = "X-Limit";
    public const string Offset = "X-Offset";
    public const string CorrelationId = "X-Correlation-Id";
    public const string IdempotentReplay = "X-Idempotent-Replay";

    public static string[] ExposedHeaders =>
    [
        TotalCount,
        Limit,
        Offset,
        CorrelationId,
        IdempotentReplay
    ];
}
