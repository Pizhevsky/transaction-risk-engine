using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TransactionRiskEngine.Api.Data;
using TransactionRiskEngine.Api.Endpoints;
using TransactionRiskEngine.Api.Infrastructure;
using TransactionRiskEngine.Api.Services.Graph;
using TransactionRiskEngine.Api.Services.Outbox;
using TransactionRiskEngine.Api.Services.Risk;
using TransactionRiskEngine.Api.Services.Transactions;

namespace TransactionRiskEngine.Api.Startup;

public static class ApplicationStartupExtensions {
    private const string FrontendCorsPolicy = "frontend";
    private const string OutboxHttpClientName = "outbox-publisher";

    public static IServiceCollection AddApiDefaults(
        this IServiceCollection services,
        IConfiguration configuration
    ) {
        services.AddOpenApi();
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddRateLimiter(options => ConfigureWriteRateLimiter(options, configuration));
        services.AddCors(options => ConfigureFrontendCors(options, configuration));

        return services;
    }

    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration
    ) {
        var connectionString = configuration.GetConnectionString("RiskDb")
            ?? throw new InvalidOperationException("Connection string 'RiskDb' is not configured.");

        services.AddDbContextFactory<AppDbContext>(options => {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped(provider =>
            provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext()
        );

        return services;
    }

    public static IServiceCollection AddRiskEngine(
        this IServiceCollection services,
        IConfiguration configuration
    ) {
        services.Configure<RiskRuleCatalogOptions>(configuration.GetSection("RiskRules"));
        services.Configure<GraphRiskOptions>(configuration.GetSection("GraphRisk"));

        services.AddScoped<IRiskScoringService, RiskScoringService>();
        services.AddScoped<ITransactionEntityResolver, TransactionEntityResolver>();
        services.AddScoped<IRiskSignalBuilder, RiskSignalBuilder>();
        services.AddScoped<IRiskRuleCatalog, RiskRuleCatalog>();
        services.AddScoped<IRiskEvaluationService, RiskEvaluationService>();
        services.AddScoped<RiskEvaluationRecordProcessor>();
        services.AddScoped<IUserGraphService, UserGraphService>();
        services.AddScoped<ITransactionGraphService, TransactionGraphService>();
        services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();

        return services;
    }

    public static IServiceCollection AddOutbox(
        this IServiceCollection services,
        IConfiguration configuration
    ) {
        services.Configure<OutboxDispatcherOptions>(configuration.GetSection("Outbox"));
        services.Configure<OutboxPublisherOptions>(configuration.GetSection("Outbox:Publisher"));

        services.AddHttpClient(OutboxHttpClientName)
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(8));
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddSingleton<LoggingOutboxPublisher>();
        services.AddSingleton<HttpOutboxPublisher>();
        services.AddSingleton<IOutboxPublisher, ConfigurableOutboxPublisher>();
        services.AddSingleton<IOutboxDeliveryService, OutboxDeliveryService>();
        services.AddSingleton<IOutboxClaimService, OutboxClaimService>();
        services.AddHostedService<OutboxDispatcherBackgroundService>();

        return services;
    }

    public static WebApplication UseApiPipeline(this WebApplication app) {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseRateLimiter();
        app.UseCors(FrontendCorsPolicy);

        if (app.Environment.IsDevelopment()) {
            app.MapOpenApi();
        }

        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app) {
        app.MapGet("/", () => Results.Ok(new {
            name = "TransactionRiskEngine",
            version = "0.5.0",
            openApi = "/openapi/v1.json"
        }));

        app.MapHealthEndpoints();
        app.MapTransactionEndpoints();
        app.MapUserEndpoints();
        app.MapFraudCaseEndpoints();
        app.MapRuleEndpoints();

        return app;
    }

    private static void ConfigureWriteRateLimiter(
        RateLimiterOptions options,
        IConfiguration configuration
    ) {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(ApiPolicyNames.WriteApi, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions {
                    PermitLimit = configuration.GetValue("RateLimiting:WritePermitLimit", 120),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    }

    private static void ConfigureFrontendCors(
        CorsOptions options,
        IConfiguration configuration
    ) {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? ["http://localhost:4200", "http://127.0.0.1:4200"];

        options.AddPolicy(FrontendCorsPolicy, policy => {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(ApiHeaderNames.ExposedHeaders);
        });
    }
}
