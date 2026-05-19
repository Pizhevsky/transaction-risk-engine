using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace TransactionRiskEngine.Tests;

public sealed class TransactionRiskEngineApiFactory : WebApplicationFactory<Program>, IAsyncLifetime {
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:14-alpine")
        .WithDatabase("transaction_risk_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync() {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.ConfigureAppConfiguration((_, config) => {
            config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:RiskDb"] = _postgres.GetConnectionString(),
                ["SeedData:Enabled"] = "true",
                ["Outbox:Enabled"] = "false"
            });
        });
    }
}
