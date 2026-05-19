namespace TransactionRiskEngine.Api.Services.Graph;

public sealed class GraphRiskOptions {
    public int MaxDepth { get; set; } = 3;
    public int MaxPaths { get; set; } = 3;
    public int CacheSeconds { get; set; } = 10;
    public int MaxEdgesPerExpansion { get; set; } = 500;
    public int MaxTotalNodes { get; set; } = 1000;
}
