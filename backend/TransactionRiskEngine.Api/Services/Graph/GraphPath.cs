namespace TransactionRiskEngine.Api.Services.Graph;

public sealed record GraphPath(
    IReadOnlyList<string> Nodes,
    string TargetLabel,
    bool TargetIsRisky
) {
    public string RiskLabel => TargetLabel;
    public bool IsRisky => TargetIsRisky;
}
