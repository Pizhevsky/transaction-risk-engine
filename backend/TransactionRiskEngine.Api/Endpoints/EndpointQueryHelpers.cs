using TransactionRiskEngine.Api.Startup;

namespace TransactionRiskEngine.Api.Endpoints;

internal static class EndpointQueryHelpers {
    private const int DefaultLimit = 100;
    private const int MaxLimit = 250;

    public static (int Limit, int Offset) ResolvePaging(int? limit, int? offset) {
        return (
            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit),
            Math.Max(offset ?? 0, 0)
        );
    }

    public static void WritePaginationHeaders(
        HttpResponse response,
        int total,
        int limit,
        int offset
    ) {
        response.Headers[ApiHeaderNames.TotalCount] = total.ToString();
        response.Headers[ApiHeaderNames.Limit] = limit.ToString();
        response.Headers[ApiHeaderNames.Offset] = offset.ToString();
    }

    public static string ContainsPattern(string value) {
        return $"%{EscapeLikePattern(value.Trim())}%";
    }

    private static string EscapeLikePattern(string value) {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
