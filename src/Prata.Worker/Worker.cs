using Prata.Infrastructure;

namespace Prata.Worker;

public sealed class Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                var processed = await processor.ProcessPendingAsync(stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("Outbox processou {Count} mensagens", processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao processar outbox");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
