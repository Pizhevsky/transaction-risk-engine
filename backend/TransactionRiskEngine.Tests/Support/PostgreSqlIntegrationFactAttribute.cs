namespace TransactionRiskEngine.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PostgreSqlIntegrationFactAttribute : FactAttribute {
    public PostgreSqlIntegrationFactAttribute() {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase
        )) {
            Skip = "Set RUN_POSTGRES_INTEGRATION_TESTS=true to run PostgreSQL/Testcontainers integration tests.";
        }
    }
}
