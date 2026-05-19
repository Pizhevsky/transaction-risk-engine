using TransactionRiskEngine.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiDefaults(builder.Configuration)
    .AddPersistence(builder.Configuration)
    .AddRiskEngine(builder.Configuration)
    .AddOutbox(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();
app.MapApiEndpoints();

await app.InitialiseDatabaseAsync();

await app.RunAsync();
