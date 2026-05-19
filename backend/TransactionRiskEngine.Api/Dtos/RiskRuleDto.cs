namespace TransactionRiskEngine.Api.Dtos;

public sealed record RiskRuleDto(
    string Code,
    string Description,
    int Weight,
    bool Enabled
);

public sealed record UpdateRiskRuleRequest(
    string? Description,
    int Weight,
    bool Enabled
);

public sealed record RiskRuleUpdateResponse(
    string Code,
    string Description,
    int Weight,
    bool Enabled,
    string Message
);

public sealed record RiskEvaluationRequest(
    int BatchSize = 250,
    string Reason = "Manual rule evaluation"
);

public sealed record RiskEvaluationResponse(
    Guid JobId,
    int ProcessedCount,
    int ChangedCount
);
