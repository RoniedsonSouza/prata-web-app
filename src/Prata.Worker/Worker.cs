using Prata.Infrastructure;
using Prata.Infrastructure.Jobs;

namespace Prata.Worker;

public sealed class Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger) : BackgroundService
{
    private DateOnly? _lastQuoteExpirationDay;
    private DateOnly? _lastSensitivePurgeDay;
    private DateOnly? _lastReconcileDay;

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

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (_lastQuoteExpirationDay != today)
                {
                    var expire = scope.ServiceProvider.GetRequiredService<IExpireQuotesProcessor>();
                    var expired = await expire.ExpirarAsync(stoppingToken);
                    _lastQuoteExpirationDay = today;
                    if (expired > 0)
                    {
                        logger.LogInformation("Expirou {Count} orcamentos", expired);
                    }
                }

                if (_lastSensitivePurgeDay != today)
                {
                    var purge = scope.ServiceProvider.GetRequiredService<IPurgeSensitiveBriefingProcessor>();
                    var purged = await purge.ExpurgarAsync(stoppingToken);
                    _lastSensitivePurgeDay = today;
                    if (purged > 0)
                    {
                        logger.LogInformation("Expurgou {Count} respostas sensiveis", purged);
                    }
                }

                if (_lastReconcileDay != today)
                {
                    var reconcile = scope.ServiceProvider.GetRequiredService<IReconcilePaymentsProcessor>();
                    var n = await reconcile.ProcessAsync(stoppingToken);
                    _lastReconcileDay = today;
                    if (n > 0)
                        logger.LogInformation("Conciliação abriu {Count} divergencias", n);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha no worker");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
