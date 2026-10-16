using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Dapper;
using NexoApi.Common.Data;

namespace NexoApi.Common.Middleware;

// Auditoria centralizada (agosto 2026, recomendacion de docs/AUDITORIA_2026.md
// seccion 8.4) -- antes Auditoria.LogAuditoria existia en la BD pero NINGUN
// servicio la escribia. En vez de repetir "cada servicio hace su propio
// INSERT" (no escala, se olvida en el modulo 21), un unico middleware
// intercepta TODA escritura (POST/PUT/PATCH/DELETE) a /api/*, sin que ningun
// desarrollador tenga que acordarse de llamarlo.
//
// Alcance deliberadamente simple: registra metodo+ruta+usuario+payload
// (ValoresNuevos), NO un diff campo-por-campo contra el valor anterior en BD
// (ValoresAnteriores queda NULL) -- eso requeriria leer el registro antes de
// cada operacion, acoplado a cada tabla. Para "quien cambio esto y cuando"
// (la pregunta real que motivo esto) es suficiente; si en el futuro se
// necesita un diff exacto campo-por-campo, hay que instrumentar por servicio.
public class AuditoriaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditoriaMiddleware> _logger;

    private static readonly HashSet<string> MetodosAuditados = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    // Rutas que nunca se auditan -- login/registro llevan contraseña en el
    // body y no representan un cambio de datos de negocio.
    private static readonly string[] RutasExcluidas = { "/api/auth/login", "/api/auth/registrar" };

    // Nombres de campo que jamas deben quedar en texto plano en el log,
    // sin importar en que endpoint aparezcan.
    private static readonly string[] CamposSensibles = { "password", "contrasena", "contraseña", "clave", "apikey", "token" };

    private const int MaxLongitudPayload = 4000;

    public AuditoriaMiddleware(RequestDelegate next, ILogger<AuditoriaMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDbConnectionFactory db)
    {
        var ruta = context.Request.Path.Value ?? "";
        // Requests del agente de sincronización (ApiKey scheme) no se auditan:
        // generan decenas de INSERTs/UPDATEs por ciclo y llenan LogAuditoria con ruido interno.
        var esAgente = context.User.Identity?.AuthenticationType == "ApiKey";

        var esAuditable = !esAgente
            && ruta.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && MetodosAuditados.Contains(context.Request.Method)
            && !RutasExcluidas.Any(r => ruta.StartsWith(r, StringComparison.OrdinalIgnoreCase));

        string? payload = null;

        if (esAuditable)
        {
            context.Request.EnableBuffering();
            using var lector = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var cuerpo = await lector.ReadToEndAsync();
            context.Request.Body.Position = 0;
            payload = RedactarSensibles(cuerpo);
        }

        await _next(context);

        // Solo se audita si la operacion realmente tuvo efecto (2xx) -- un
        // 400/401/403/404/409 no cambio nada en la base, no vale la pena registrarlo.
        if (esAuditable && context.Response.StatusCode is >= 200 and < 300)
        {
            try
            {
                await RegistrarAsync(db, context, ruta, payload);
            }
            catch (Exception ex)
            {
                // La auditoria nunca debe tumbar la request real -- si falla
                // el INSERT (ej. BD momentaneamente no disponible), se registra
                // el error en el log de la app y se sigue sin afectar al usuario.
                _logger.LogWarning(ex, "No se pudo registrar la auditoria para {Ruta}", ruta);
            }
        }
    }

    private static async Task RegistrarAsync(IDbConnectionFactory db, HttpContext context, string ruta, string? payload)
    {
        var usuarioIdTexto = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? usuarioId = int.TryParse(usuarioIdTexto, out var id) ? id : null;
        var nombreUsuario = context.User.FindFirstValue(ClaimTypes.Name)
                         ?? context.User.FindFirstValue("nombre")
                         ?? context.User.FindFirstValue(ClaimTypes.Email);

        // IP: preferir X-Forwarded-For (detrás de proxy/nginx) sobre RemoteIpAddress
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
              ?? context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ip) && ip.Contains(','))
            ip = ip.Split(',')[0].Trim(); // solo la primera IP del chain

        var segmentos = ruta.Trim('/').Split('/');
        var registroId = segmentos.LastOrDefault(s => int.TryParse(s, out _)) ?? "nuevo";

        using var connection = db.CreateConnection();
        const string sql = @"
            INSERT INTO Auditoria.LogAuditoria (EsquemaTabla, RegistroID, Accion, UsuarioID, NombreUsuario, IP, ValoresNuevos)
            VALUES (@EsquemaTabla, @RegistroID, @Accion, @UsuarioID, @NombreUsuario, @IP, @ValoresNuevos)";

        await connection.ExecuteAsync(sql, new
        {
            EsquemaTabla = ruta,
            RegistroID = registroId,
            Accion = MapearAccion(context.Request.Method),
            UsuarioID = usuarioId,
            NombreUsuario = nombreUsuario,
            IP = ip,
            ValoresNuevos = payload
        });
    }

    // Auditoria.LogAuditoria.Accion tiene un CHECK que solo acepta
    // 'INSERT'/'UPDATE'/'DELETE' (verbos de base de datos, no HTTP) --
    // confirmado contra la tabla real antes de asumir el mapeo.
    private static string MapearAccion(string metodoHttp) => metodoHttp.ToUpperInvariant() switch
    {
        "POST" => "INSERT",
        "PUT" or "PATCH" => "UPDATE",
        "DELETE" => "DELETE",
        _ => "UPDATE"
    };

    private static string? RedactarSensibles(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        if (json.Length > MaxLongitudPayload * 4) // demasiado grande incluso para intentar parsear (ej. base64 de un documento)
            return "(payload omitido -- demasiado grande, probablemente un archivo adjunto)";

        try
        {
            var nodo = JsonNode.Parse(json);
            RedactarNodo(nodo);
            var resultado = nodo?.ToJsonString() ?? json;
            return resultado.Length > MaxLongitudPayload ? resultado[..MaxLongitudPayload] + "…" : resultado;
        }
        catch
        {
            // Si no es JSON valido (no deberia pasar en esta API, pero por
            // seguridad), se trunca el texto crudo en vez de fallar.
            return json.Length > MaxLongitudPayload ? json[..MaxLongitudPayload] + "…" : json;
        }
    }

    private static void RedactarNodo(JsonNode? nodo)
    {
        if (nodo is JsonObject obj)
        {
            foreach (var propiedad in obj.ToList())
            {
                if (CamposSensibles.Any(c => propiedad.Key.Contains(c, StringComparison.OrdinalIgnoreCase)))
                    obj[propiedad.Key] = "***";
                else
                    RedactarNodo(propiedad.Value);
            }
        }
        else if (nodo is JsonArray arr)
        {
            foreach (var item in arr)
                RedactarNodo(item);
        }
    }
}
