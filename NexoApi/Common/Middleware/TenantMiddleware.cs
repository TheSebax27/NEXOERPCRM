using NexoApi.Common.Data;

namespace NexoApi.Common.Middleware;

/// <summary>
/// Middleware que extrae el subdominio del Host header, resuelve el connection
/// string del cliente en admin_services y lo guarda en HttpContext.Items
/// para que TenantSqlConnectionFactory lo use de forma sincronica.
/// En localhost usa la conexion local de appsettings (modo desarrollo).
/// </summary>
public class TenantMiddleware
{
    public const string ConnectionStringKey = "Nexo_TenantConnectionString";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        TenantConnectionService tenantService,
        IConfiguration configuration)
    {
        // X-Nexo-Host lo inyecta TenantHostForwardingHandler de NexoWeb cuando
        // Web y API están en servidores distintos, para no pisar el Host header
        // que IIS necesita para enrutar al binding correcto.
        var host = context.Request.Headers["X-Nexo-Host"].FirstOrDefault()
                ?? context.Request.Host.Host;

        context.Items["Nexo_TenantHost"] = host;

        // Endpoints sin tenant ni DB (accedidos directamente por el navegador sin X-Nexo-Host)
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/utils/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/utils", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (EsLocalhost(host))
        {
            // Busca la primera ConnectionString de NEXO no vacía (ignora NexoDb="" de producción).
            static string? GetConn(IConfiguration cfg, string key)
            {
                var v = cfg.GetConnectionString(key);
                return string.IsNullOrWhiteSpace(v) ? null : v;
            }

            var local = GetConn(configuration, "NexoDb")
                     ?? GetConn(configuration, "NexoDB")
                     ?? GetConn(configuration, "DefaultConnection")
                     ?? configuration.GetSection("ConnectionStrings").GetChildren()
                            .FirstOrDefault(c => !c.Key.Equals("AdminDb", StringComparison.OrdinalIgnoreCase)
                                              && !string.IsNullOrWhiteSpace(c.Value))?.Value
                     ?? throw new InvalidOperationException(
                            "No hay ninguna ConnectionString de NEXO configurada para modo desarrollo (localhost/IP directa).");
            context.Items[ConnectionStringKey] = local;
        }
        else
        {
            // "vecco.insumar.com.co" -> "vecco"
            var subdomain = host.Split('.')[0];

            try
            {
                var connStr = await tenantService.ResolveAsync(subdomain);
                context.Items[ConnectionStringKey] = connStr;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Tenant rechazado — Host: {Host}, Subdominio: {Subdomain}, Razón: {Mensaje}",
                    host, subdomain, ex.Message);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error   = ex.Message,
                    detalle = $"Subdominio evaluado: '{subdomain}'. Verifica en admin_services que exista el registro con service_id=13, active_service=1 y la empresa no esté bloqueada."
                });
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al resolver tenant '{Subdomain}' desde admin_services", subdomain);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new
                {
                    error   = "No se pudo verificar el acceso del cliente. El servicio de registro está temporalmente no disponible.",
                    detalle = $"Subdominio: '{subdomain}'. Contacta al administrador si el problema persiste."
                });
                return;
            }
        }

        await _next(context);
    }

    private static bool EsLocalhost(string host) =>
        host is "localhost" or "127.0.0.1" or "::1"
        || System.Net.IPAddress.TryParse(host, out _); // IP directa = dev/swagger sin subdominio
}
