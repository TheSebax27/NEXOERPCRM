using NexoApi.Features.Crm;
using NexoApi.Features.Produccion;

namespace NexoApi.BackgroundServices;

public class NexoScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NexoScheduler> _logger;

    public NexoScheduler(IServiceScopeFactory scopeFactory, ILogger<NexoScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var ahora = NexoApi.Common.ColombiaClock.Now;
            var proxima = NexoApi.Common.ColombiaClock.Today.AddDays(ahora.Hour >= 6 ? 1 : 0).AddHours(6);
            var espera = proxima - ahora;
            await Task.Delay(espera, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("NexoScheduler: iniciando tareas diarias {Fecha}", NexoApi.Common.ColombiaClock.Now);
            await EjecutarTareasAsync();
        }
    }

    private async Task EjecutarTareasAsync()
    {
        using var scope = _scopeFactory.CreateScope();

        try
        {
            var opService = scope.ServiceProvider.GetRequiredService<IOrdenesProduccionService>();
            var retrasadas = await opService.MarcarRetrasadasAsync();
            _logger.LogInformation("NexoScheduler: {N} OP(s) marcadas como Retrasada", retrasadas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NexoScheduler: error al marcar OPs retrasadas");
        }

        try
        {
            var crmService = scope.ServiceProvider.GetRequiredService<ICrmService>();
            var expiradas = await crmService.ExpireCotizacionesVencidasAsync();
            _logger.LogInformation("NexoScheduler: {N} cotización(es) expirada(s)", expiradas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NexoScheduler: error al expirar cotizaciones");
        }
    }
}
