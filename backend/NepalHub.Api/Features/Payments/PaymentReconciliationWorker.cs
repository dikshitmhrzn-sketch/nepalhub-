namespace NepalHub.Api.Features.Payments;

/// <summary>
/// Puts the stale-payment sweep on a timer, so a checkout that was paid but whose buyer never
/// came back settles on its own rather than waiting for the buyer or an administrator to
/// remember it exists. The loop waits first, so booting never sweeps; a pass that throws is
/// logged and the next interval tries again rather than the worker dying with the host's
/// least interesting background task. <c>Payments:ReconcileIntervalSeconds</c> of zero — the
/// value tests and hosts without a provider run with — ends the worker before its first wait.
/// </summary>
public sealed class PaymentReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    PaymentSettings settings,
    ILogger<PaymentReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(settings.ReconcileIntervalSeconds);

        if (interval <= TimeSpan.Zero)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<PaymentService>();

                var examined = await paymentService.ReconcileStaleAsync(stoppingToken);
                if (examined > 0)
                {
                    logger.LogInformation(
                        "Reconciled {Examined} stale pending payment(s) against the provider.",
                        examined);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The stale payment sweep failed; the next interval will retry.");
            }
        }
    }
}
