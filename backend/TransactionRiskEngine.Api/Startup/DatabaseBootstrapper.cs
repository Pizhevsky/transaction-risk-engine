using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;

namespace TransactionRiskEngine.Api.Startup;

public static class DatabaseBootstrapper {
    public static async Task InitialiseDatabaseAsync(this WebApplication app) {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var useMigrations = app.Configuration.GetValue<bool>("Database:UseMigrations");

        if (useMigrations) {
            await EnsureMigrationHistoryTableAsync(db);
            await db.Database.MigrateAsync();
        } else {
            await db.Database.EnsureCreatedAsync();
        }

        await RiskRuleBootstrapper.EnsureDefaultRiskRulesAsync(db);

        if (app.Configuration.GetValue<bool>("SeedData:Enabled")) {
            await SeedData.InitialiseAsync(db);
        }
    }

    private static async Task EnsureMigrationHistoryTableAsync(AppDbContext db) {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true) {
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """);
    }
}
