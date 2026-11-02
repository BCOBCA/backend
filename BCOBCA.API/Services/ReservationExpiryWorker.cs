using BCOBCA.BLL.Interfaces;

namespace BCOBCA.API.Services;

public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.GetValue<int>("Inventory:SweepIntervalSeconds")));
        do
        {
            try
            {
                for (var count = 0; count < 100 && !stoppingToken.IsCancellationRequested; count++)
                {
                    // A failed Serializable transaction must not reuse a tracked DbContext.
                    using var scope = scopes.CreateScope();
                    if (await scope.ServiceProvider.GetRequiredService<IOrderService>().ExpireOneReservationAsync(stoppingToken) == 0) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning("Reservation sweep will retry; failure type: {ExceptionType}", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
