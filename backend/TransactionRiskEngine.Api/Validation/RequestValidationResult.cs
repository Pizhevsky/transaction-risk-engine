namespace TransactionRiskEngine.Api.Validation;

public sealed record RequestValidationResult(
    bool IsValid,
    IDictionary<string, string[]> Errors
) {
    public static RequestValidationResult Success() => new(true, new Dictionary<string, string[]>());

    public static RequestValidationResult Failure(Dictionary<string, List<string>> errors) =>
        new(false, errors.ToDictionary(x => x.Key, x => x.Value.ToArray()));
}
