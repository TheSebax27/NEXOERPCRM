using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dapper;
using NexoApi.Common.Data;

namespace NexoApi.Features.Email;

public class BrevoEmailService(IDbConnectionFactory db, IHttpClientFactory http) : IEmailService
{
    private readonly IDbConnectionFactory _db = db;

    public async Task<EmailConfigItem> ObtenerConfigAsync()
    {
        using var conn = _db.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<ConfigRow>(
            "SELECT Proveedor, ApiKey, EmailFrom, NombreFrom, Activo FROM Organizacion.ConfiguracionEmail WHERE ConfiguracionID = 1");
        return row is null
            ? new EmailConfigItem("Brevo", null, null, null, false)
            : new EmailConfigItem(row.Proveedor, row.ApiKey, row.EmailFrom, row.NombreFrom, row.Activo);
    }

    public async Task GuardarConfigAsync(EmailConfigItem config)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM Organizacion.ConfiguracionEmail WHERE ConfiguracionID = 1)
                INSERT INTO Organizacion.ConfiguracionEmail (ConfiguracionID, Proveedor, ApiKey, EmailFrom, NombreFrom, Activo, EmailsHoy, FechaModificacion)
                VALUES (1, 'Sin configurar', NULL, NULL, NULL, 0, 0, SYSUTCDATETIME())
            """);
        await conn.ExecuteAsync("""
            UPDATE Organizacion.ConfiguracionEmail
            SET Proveedor = @Proveedor, ApiKey = @ApiKey, EmailFrom = @EmailFrom,
                NombreFrom = @NombreFrom, Activo = @Activo, FechaModificacion = SYSUTCDATETIME()
            WHERE ConfiguracionID = 1
            """, config);
    }

    public async Task<bool> SendAsync(EmailMessage message)
    {
        var config = await ObtenerConfigAsync();
        if (!config.Activo || string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.EmailFrom))
            return false;

        var payload = new
        {
            sender = new
            {
                email = config.EmailFrom,
                name  = string.IsNullOrWhiteSpace(config.NombreFrom) ? config.EmailFrom : config.NombreFrom
            },
            to = new[]
            {
                new
                {
                    email = message.To,
                    name  = string.IsNullOrWhiteSpace(message.ToName) ? message.To : message.ToName
                }
            },
            subject     = message.Subject,
            htmlContent = message.Html
        };

        var client  = http.CreateClient();
        client.DefaultRequestHeaders.Add("api-key", config.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var json    = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp    = await client.PostAsync("https://api.brevo.com/v3/smtp/email", content);
        return resp.IsSuccessStatusCode;
    }

    private record ConfigRow(string Proveedor, string? ApiKey, string? EmailFrom, string? NombreFrom, bool Activo);
}
