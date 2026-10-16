using Dapper;
using NexoApi.Common.Data;

namespace NexoApi.Common.BackgroundServices;

// Elimina registros de Auditoria.LogAuditoria mayores a 7 días cada 24 horas.
// Evita que la tabla crezca sin límite y sature el MDF (caso nexoVisions oct-2026).
public class AuditoriaPurgeService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditoriaPurgeService> _logger;
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);
    private static readonly int DiasRetencion = 7;

    public AuditoriaPurgeService(IServiceScopeFactory scopeFactory, ILogger<AuditoriaPurgeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Esperar 5 minutos al arrancar para no competir con el startup.
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al purgar LogAuditoria");
            }

            await Task.Delay(Intervalo, stoppingToken);
        }
    }

    private async Task PurgarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        using var connection = db.CreateConnection();
        const string sql = @"
            DELETE FROM Auditoria.LogAuditoria
            WHERE Fecha < DATEADD(DAY, -@Dias, GETUTCDATE())";

        var eliminados = await connection.ExecuteAsync(sql, new { Dias = DiasRetencion });

        if (eliminados > 0)
            _logger.LogInformation("AuditoriaPurge: {Eliminados} registros eliminados (>{Dias} días)", eliminados, DiasRetencion);
    }
}
