using BCOBCA.BLL.Services;
namespace BCOBCA.API.Services;

public sealed class CommerceDeliveryWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<CommerceDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await Task.WhenAll(RunAsync(true, stoppingToken), RunAsync(false, stoppingToken));

    private async Task RunAsync(bool shipping, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            if (config.GetValue<bool>(shipping ? "Shipping:Ghn:FulfillmentEnabled" : "Push:Enabled"))
            {
                try
                {
                    for (var count = 0; count < 20 && !stoppingToken.IsCancellationRequested; count++)
                    {
                        using var scope = scopes.CreateScope();
                        var worked = shipping ? await scope.ServiceProvider.GetRequiredService<GhnFulfillmentService>().CreateOneAsync(stoppingToken)
                            : await scope.ServiceProvider.GetRequiredService<PushDeliveryService>().DeliverOneAsync(stoppingToken);
                        if (!worked) break;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogWarning("Commerce delivery will retry; failure type: {ExceptionType}", ex.GetType().Name); }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
