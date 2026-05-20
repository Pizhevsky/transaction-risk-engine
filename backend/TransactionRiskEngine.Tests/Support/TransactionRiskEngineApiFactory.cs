using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
                ["SeedData:Enabled"] = "true",
                ["Outbox:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services => {
            services.RemoveAll<IDbContextFactory<AppDbContext>>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContextFactory<AppDbContext>(options => {
                options.UseNpgsql(_postgres.GetConnectionString());
            });

            services.AddScoped(provider =>
                provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext()
            );
        });
    }
}