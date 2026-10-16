using NexoApi.Features.Crm;

namespace NexoApi.Infrastructure;

// Evalúa las reglas periódicas de automatización CRM cada hora.
// En modo multi-tenant (producción sin NexoDb fijo) este servicio se deshabilita
// automaticamente porque no hay un tenant asignado para el contexto de fondo.
public class AutomationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AutomationBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public AutomationBackgroundService(
        IServiceProvider services,
        ILogger<AutomationBackgroundService> logger,
        IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // En multi-tenant (sin NexoDb fijo) no hay contexto de tenant para el background.
        // El servicio se omite silenciosamente hasta que se implemente soporte multi-tenant
        // para background jobs (iterar todos los tenants activos desde admin_services).
        if (string.IsNullOrEmpty(_configuration.GetConnectionString("NexoDb")))
        {
            _logger.LogInformation("AutomationBackgroundService deshabilitado: sin NexoDb fijo (modo multi-tenant).");
            return;
        }

        // Primera ejecución 5 minutos tras el arranque, luego cada hora.
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var automation = scope.ServiceProvider.GetRequiredService<IAutomacionService>();
                await automation.EvaluarReglasPeriodicasAsync();
                _logger.LogInformation("Automatización CRM: evaluación periódica completada ({Hora})", NexoApi.Common.ColombiaClock.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AutomationBackgroundService");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
