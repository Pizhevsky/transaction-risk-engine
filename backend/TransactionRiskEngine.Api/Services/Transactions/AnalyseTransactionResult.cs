using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Transactions;

public sealed record AnalyseTransactionResult(
    AnalyseTransactionResponse? Response,
    bool IsReplay,
    bool HasConflict = false,
    string? ConflictDetail = null
) {
    public static AnalyseTransactionResult Created(AnalyseTransactionResponse response) =>
        new(response, IsReplay: false);

    public static AnalyseTransactionResult Replay(AnalyseTransactionResponse response) =>
        new(response, IsReplay: true);

    public static AnalyseTransactionResult Conflict(string detail) =>
        new(null, IsReplay: false, HasConflict: true, ConflictDetail: detail);
}
