using System.Net.Http.Headers;
using System.Text;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Crm.Dtos;
using NexoApi.Features.WhatsApp.Dtos;

namespace NexoApi.Features.WhatsApp;

public interface IWhatsAppService
{
    Task<WhatsAppConfigItem> ObtenerConfigAsync();
    Task GuardarConfigAsync(WhatsAppConfigItem config);
    Task<WhatsAppResponse> EnviarAsync(string telefono, string mensaje);

    Task<IEnumerable<NumeroWhatsAppItem>> ListarNumerosAsync();
    Task<int> CrearNumeroAsync(CrearNumeroWhatsAppRequest req);
    Task ToggleNumeroAsync(int numeroWaId);
    Task EliminarNumeroAsync(int numeroWaId);
}

public class WhatsAppService(HttpClient http, IDbConnectionFactory db) : IWhatsAppService
{
    public async Task<WhatsAppConfigItem> ObtenerConfigAsync()
    {
        using var conn = db.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<ConfigRow>(
            "SELECT AccountSid, AuthToken, FromNumber, Activo FROM Organizacion.ConfiguracionWhatsApp WHERE ConfiguracionID = 1");
        return row is null
            ? new WhatsAppConfigItem(null, null, "whatsapp:+14155238886", false)
            : new WhatsAppConfigItem(row.AccountSid, row.AuthToken, row.FromNumber, row.Activo);
    }

    public async Task GuardarConfigAsync(WhatsAppConfigItem config)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM Organizacion.ConfiguracionWhatsApp WHERE ConfiguracionID = 1)
                INSERT INTO Organizacion.ConfiguracionWhatsApp (ConfiguracionID, AccountSid, AuthToken, FromNumber, Activo, FechaModificacion)
                VALUES (1, NULL, NULL, 'whatsapp:+14155238886', 0, SYSUTCDATETIME())
            """);
        await conn.ExecuteAsync("""
            UPDATE Organizacion.ConfiguracionWhatsApp
            SET AccountSid = @AccountSid, AuthToken = @AuthToken,
                FromNumber = @FromNumber, Activo = @Activo,
                FechaModificacion = SYSUTCDATETIME()
            WHERE ConfiguracionID = 1
            """, config);
    }

    public async Task<WhatsAppResponse> EnviarAsync(string telefono, string mensaje)
    {
        var config = await ObtenerConfigAsync();

        if (!config.Activo || string.IsNullOrWhiteSpace(config.AccountSid) || string.IsNullOrWhiteSpace(config.AuthToken))
            return new WhatsAppResponse(false, null, "WhatsApp no configurado o inactivo. Ve a Configuración → WhatsApp para activarlo.");

        var to = telefono.StartsWith("whatsapp:") ? telefono : $"whatsapp:+57{telefono.TrimStart('0').TrimStart('+')}";
        var url = $"https://api.twilio.com/2010-04-01/Accounts/{config.AccountSid}/Messages.json";
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{config.AccountSid}:{config.AuthToken}"));

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = config.FromNumber,
            ["To"]   = to,
            ["Body"] = mensaje,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

        try
        {
            var response = await http.SendAsync(request);
            var body     = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var sid = System.Text.Json.JsonDocument.Parse(body).RootElement
                              .GetProperty("sid").GetString();
                return new WhatsAppResponse(true, sid, null);
            }
            return new WhatsAppResponse(false, null, body);
        }
        catch (Exception ex)
        {
            return new WhatsAppResponse(false, null, ex.Message);
        }
    }

    public async Task<IEnumerable<NumeroWhatsAppItem>> ListarNumerosAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<NumeroWhatsAppItem>(@"
            SELECT n.NumeroWAID, n.Descripcion, n.Numero,
                   n.EmpleadoID,
                   ISNULL(e.Nombres + ' ' + e.Apellidos, NULL) AS NombreEmpleado,
                   n.CentroCostoID,
                   cc.Nombre AS NombreCentroCosto,
                   n.Activo
            FROM Organizacion.NumerosWhatsApp n
            LEFT JOIN Rrhh.Empleados e ON e.EmpleadoID = n.EmpleadoID
            LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = n.CentroCostoID
            ORDER BY n.Descripcion");
    }

    public async Task<int> CrearNumeroAsync(CrearNumeroWhatsAppRequest req)
    {
        using var conn = db.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Organizacion.NumerosWhatsApp (Descripcion, Numero, EmpleadoID, CentroCostoID)
            OUTPUT INSERTED.NumeroWAID
            VALUES (@Descripcion, @Numero, @EmpleadoID, @CentroCostoID)",
            req);
    }

    public async Task ToggleNumeroAsync(int numeroWaId)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Organizacion.NumerosWhatsApp SET Activo = ~Activo WHERE NumeroWAID = @id",
            new { id = numeroWaId });
    }

    public async Task EliminarNumeroAsync(int numeroWaId)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM Organizacion.NumerosWhatsApp WHERE NumeroWAID = @id",
            new { id = numeroWaId });
    }

    private record ConfigRow(string? AccountSid, string? AuthToken, string FromNumber, bool Activo);
}
