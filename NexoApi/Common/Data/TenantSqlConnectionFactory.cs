using System.Data;
using Microsoft.Data.SqlClient;
using NexoApi.Common.Middleware;

namespace NexoApi.Common.Data;

/// <summary>
/// Implementacion de IDbConnectionFactory que resuelve la BD del cliente
/// leyendo el connection string que TenantMiddleware dejó en HttpContext.Items.
/// En background services (sin HttpContext) cae al fallback local NexoDb.
/// </summary>
public class TenantSqlConnectionFactory : IDbConnectionFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public TenantSqlConnectionFactory(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public IDbConnection CreateConnection()
    {
        var ctx = _httpContextAccessor.HttpContext;

        string connStr;

        if (ctx is not null)
        {
            // Camino normal: TenantMiddleware ya resolvio el connection string
            connStr = ctx.Items[TenantMiddleware.ConnectionStringKey] as string
                ?? throw new InvalidOperationException(
                    "El connection string del tenant no fue resuelto. " +
                    "Verifica que TenantMiddleware esté registrado en el pipeline.");
        }
        else
        {
            // Camino de background services (AutomationBackgroundService, etc.)
            // En produccion estos servicios deberian operar sobre la BD indicada
            // en 'NexoDb' o deshabilitarse si son multi-tenant.
            connStr = _configuration.GetConnectionString("NexoDb")
                ?? throw new InvalidOperationException(
                    "Falta 'NexoDb' en ConnectionStrings (requerido para background services).");
        }

        return new SqlConnection(connStr);
    }
}
