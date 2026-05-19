namespace TransactionRiskEngine.Api.Domain;

public enum FraudCaseStatus {
    Open = 0,
    Investigating = 1,
    ClosedApproved = 2,
    ClosedBlocked = 3
}
