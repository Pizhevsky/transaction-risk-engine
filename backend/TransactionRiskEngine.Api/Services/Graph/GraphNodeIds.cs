namespace TransactionRiskEngine.Api.Services.Graph;

internal static class GraphNodeIds {
    public const string UserPrefix = "U:";
    public const string DevicePrefix = "D:";
    public const string CardPrefix = "C:";
    public const string IpPrefix = "IP:";

    public static string User(Guid id) => $"{UserPrefix}{id}";
    public static string Device(Guid id) => $"{DevicePrefix}{id}";
    public static string Card(Guid id) => $"{CardPrefix}{id}";
    public static string Ip(Guid id) => $"{IpPrefix}{id}";
}
