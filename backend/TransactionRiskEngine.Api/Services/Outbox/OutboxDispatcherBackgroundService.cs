using Microsoft.Extensions.Options;
using TransactionRiskEngine.Api.Data;

namespace TransactionRiskEngine.Api.Services.Outbox;

public sealed class OutboxDispatcherBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxDispatcherOptions> options,
    IOutboxDeliveryService deliveryService,
    IOutboxClaimService claimService,
    ILogger<OutboxDispatcherBackgroundService> logger
) : BackgroundService {
    private readonly string workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!options.Value.Enabled) {
            logger.LogInformation("Outbox dispatcher is disabled by configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await DispatchBatchAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception ex) {
                logger.LogError(ex, "Outbox dispatch loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken) {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await claimService.RequeueStaleProcessingMessagesAsync(db, cancellationToken);

        var messages = await claimService.ClaimBatchAsync(db, workerId, cancellationToken);
        foreach (var message in messages) {
            await deliveryService.DispatchAsync(message, cancellationToken);
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
