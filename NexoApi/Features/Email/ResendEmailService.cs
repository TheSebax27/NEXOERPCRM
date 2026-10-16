using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dapper;
using NexoApi.Common.Data;

namespace NexoApi.Features.Email;

public class ResendEmailService(IDbConnectionFactory db, IHttpClientFactory http) : IEmailService
{
    public async Task<EmailConfigItem> ObtenerConfigAsync()
    {
        using var conn = _db.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<ConfigRow>(
            "SELECT Proveedor, ApiKey, EmailFrom, NombreFrom, Activo FROM Organizacion.ConfiguracionEmail WHERE ConfiguracionID = 1");
        return row is null
            ? new EmailConfigItem("Resend", null, null, null, false)
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

        var fromField = string.IsNullOrWhiteSpace(config.NombreFrom)
            ? config.EmailFrom
            : $"{config.NombreFrom} <{config.EmailFrom}>";

        var payload = new
        {
            from    = fromField,
            to      = new[] { string.IsNullOrWhiteSpace(message.ToName) ? message.To : $"{message.ToName} <{message.To}>" },
            subject = message.Subject,
            html    = message.Html
        };

        var client = http.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var json    = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp    = await client.PostAsync("https://api.resend.com/emails", content);
        return resp.IsSuccessStatusCode;
    }

    private readonly IDbConnectionFactory _db = db;

    private record ConfigRow(string Proveedor, string? ApiKey, string? EmailFrom, string? NombreFrom, bool Activo);
}
