using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Email;
using NexoApi.Features.Marketing.Dtos;

namespace NexoApi.Features.Marketing;

public interface IMarketingService
{
    // Campañas
    Task<IEnumerable<CampanaResumen>> ListarCampanasAsync();
    Task<CampanaDetalle?> ObtenerCampanaAsync(int id);
    Task<int> CrearCampanaAsync(GuardarCampanaRequest r, int usuarioId);
    Task ActualizarCampanaAsync(int id, GuardarCampanaRequest r);
    Task EliminarCampanaAsync(int id);

    // Segmentación
    Task<int> ContarDestinatariosAsync(string? tipo, string? valor);

    Task<string> ObtenerNombreEmpresaAsync();

    // Imágenes
    Task<IEnumerable<ImagenItem>> ListarImagenesAsync();
    Task<int> SubirImagenAsync(SubirImagenRequest r, int usuarioId);
    Task<(string ContentType, string Base64)?> ObtenerImagenAsync(int id);
    Task EliminarImagenAsync(int id);

    // Envío
    Task<string> EnviarPruebaAsync(int campanaId, string emailDestino);
    Task IniciarEnvioAsync(int campanaId, string apiBaseUrl);

    // Tracking (llamados desde endpoints públicos)
    Task<bool> RegistrarAperturaAsync(string token);
    Task<string?> RegistrarClickAsync(string token, string url);
    Task<bool> RegistrarDesuscripcionAsync(string token);

    // Detalle por destinatario
    Task<IEnumerable<EnvioDetalleItem>> ListarEnviosDetalleAsync(int campanaId);
}

public class MarketingService(IDbConnectionFactory db, IEmailService email) : IMarketingService
{
    // ── Campañas ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<CampanaResumen>> ListarCampanasAsync()
    {
        using var c = db.CreateConnection();
        return await c.QueryAsync<CampanaResumen>(@"
            SELECT CampanaID, Nombre, Descripcion, Asunto, Estado,
                   SegmentoTipo, SegmentoValor, TotalDestinatarios, TotalEnviados,
                   TotalAbiertos, TotalClicks, TotalDesuscriptos, FechaCreacion, FechaEnvio
            FROM Marketing.Campanas
            ORDER BY FechaCreacion DESC");
    }

    public async Task<CampanaDetalle?> ObtenerCampanaAsync(int id)
    {
        using var c = db.CreateConnection();
        return await c.QueryFirstOrDefaultAsync<CampanaDetalle>(@"
            SELECT CampanaID, Nombre, Descripcion, Asunto, BloqueJSON, Estado,
                   SegmentoTipo, SegmentoValor, TotalDestinatarios, TotalEnviados,
                   TotalAbiertos, TotalClicks, TotalDesuscriptos, FechaCreacion, FechaEnvio
            FROM Marketing.Campanas WHERE CampanaID = @Id", new { Id = id });
    }

    public async Task<int> CrearCampanaAsync(GuardarCampanaRequest r, int usuarioId)
    {
        using var c = db.CreateConnection();
        return await c.ExecuteScalarAsync<int>(@"
            INSERT INTO Marketing.Campanas (Nombre, Descripcion, Asunto, BloqueJSON, SegmentoTipo, SegmentoValor, UsuarioID)
            OUTPUT INSERTED.CampanaID
            VALUES (@Nombre, @Descripcion, @Asunto, @BloqueJSON, @SegmentoTipo, @SegmentoValor, @UsuarioID)",
            new { r.Nombre, r.Descripcion, r.Asunto, r.BloqueJSON, r.SegmentoTipo, r.SegmentoValor, UsuarioID = usuarioId });
    }

    public async Task ActualizarCampanaAsync(int id, GuardarCampanaRequest r)
    {
        using var c = db.CreateConnection();
        var filas = await c.ExecuteAsync(@"
            UPDATE Marketing.Campanas
            SET Nombre = @Nombre, Descripcion = @Descripcion, Asunto = @Asunto,
                BloqueJSON = @BloqueJSON, SegmentoTipo = @SegmentoTipo, SegmentoValor = @SegmentoValor
            WHERE CampanaID = @Id AND Estado = 'BORRADOR'",
            new { Id = id, r.Nombre, r.Descripcion, r.Asunto, r.BloqueJSON, r.SegmentoTipo, r.SegmentoValor });
        if (filas == 0) throw new InvalidOperationException("No se puede editar una campaña que ya fue enviada.");
    }

    public async Task EliminarCampanaAsync(int id)
    {
        using var c = db.CreateConnection();
        await c.ExecuteAsync("DELETE FROM Marketing.Campanas WHERE CampanaID = @Id AND Estado = 'BORRADOR'", new { Id = id });
    }

    // ── Segmentación ──────────────────────────────────────────────────────────

    public async Task<int> ContarDestinatariosAsync(string? tipo, string? valor)
    {
        using var c = db.CreateConnection();
        return await c.ExecuteScalarAsync<int>(SqlSegmento(tipo, valor, contar: true),
            new { Valor = valor, DiasSinContacto = int.TryParse(valor, out var d) ? d : 60 });
    }

    private async Task<IEnumerable<(int ClienteID, string Email, string Nombre, string? Empresa)>> ObtenerDestinatariosAsync(
        string? tipo, string? valor)
    {
        using var c = db.CreateConnection();
        return await c.QueryAsync<(int, string, string, string?)>(
            SqlSegmento(tipo, valor, contar: false),
            new { Valor = valor, DiasSinContacto = int.TryParse(valor, out var d) ? d : 60 });
    }

    private static string SqlSegmento(string? tipo, string? valor, bool contar)
    {
        var select = contar
            ? "SELECT COUNT(*) FROM Crm.Clientes cl"
            : "SELECT cl.ClienteID, cl.Email, cl.Nombre, cl.Contacto AS Empresa FROM Crm.Clientes cl";

        if (tipo == "FRIOS")
        {
            var cteSelect = contar
                ? "SELECT COUNT(*)"
                : "SELECT cl.ClienteID, cl.Email, cl.Nombre, cl.Contacto AS Empresa";
            return $"""
                WITH _UltInt AS (
                    SELECT ClienteID, MAX(Fecha) AS UltimaFecha
                    FROM Crm.Interacciones
                    GROUP BY ClienteID
                )
                {cteSelect}
                FROM Crm.Clientes cl
                LEFT JOIN _UltInt ui ON ui.ClienteID = cl.ClienteID
                WHERE cl.Estado = 1 AND cl.Email IS NOT NULL
                  AND ISNULL(ui.UltimaFecha, '2000-01-01') < DATEADD(DAY, -@DiasSinContacto, SYSUTCDATETIME())
                """;
        }

        var where = tipo switch
        {
            "TIPO_CLIENTE" => "cl.Estado = 1 AND cl.Email IS NOT NULL AND cl.TipoCliente = @Valor",
            "FUENTE"       => "cl.Estado = 1 AND cl.Email IS NOT NULL AND cl.FuenteContacto = @Valor",
            "RESPONSABLE"  => "cl.Estado = 1 AND cl.Email IS NOT NULL AND cl.ResponsableID = TRY_CAST(@Valor AS INT)",
            "CON_COTIZACION" => """
                cl.Estado = 1 AND cl.Email IS NOT NULL
                AND EXISTS (SELECT 1 FROM Crm.Cotizaciones co
                            WHERE co.ClienteID = cl.ClienteID AND co.Estado IN ('BORRADOR','ENVIADA','PENDIENTE'))
                """,
            _ => "cl.Estado = 1 AND cl.Email IS NOT NULL"   // TODOS
        };

        return $"{select} WHERE {where}";
    }

    // ── Imágenes ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ImagenItem>> ListarImagenesAsync()
    {
        using var c = db.CreateConnection();
        return await c.QueryAsync<ImagenItem>(
            "SELECT ImagenID, Nombre, ContentType, FechaSubida FROM Marketing.Imagenes ORDER BY FechaSubida DESC");
    }

    public async Task<int> SubirImagenAsync(SubirImagenRequest r, int usuarioId)
    {
        using var c = db.CreateConnection();
        return await c.ExecuteScalarAsync<int>(@"
            INSERT INTO Marketing.Imagenes (Nombre, ContentType, DatosBase64, UsuarioID)
            OUTPUT INSERTED.ImagenID
            VALUES (@Nombre, @ContentType, @DatosBase64, @UsuarioID)",
            new { r.Nombre, r.ContentType, r.DatosBase64, UsuarioID = usuarioId });
    }

    public async Task<(string ContentType, string Base64)?> ObtenerImagenAsync(int id)
    {
        using var c = db.CreateConnection();
        var row = await c.QueryFirstOrDefaultAsync<(string, string)>(
            "SELECT ContentType, DatosBase64 FROM Marketing.Imagenes WHERE ImagenID = @Id", new { Id = id });
        return row == default ? null : row;
    }

    public async Task EliminarImagenAsync(int id)
    {
        using var c = db.CreateConnection();
        await c.ExecuteAsync("DELETE FROM Marketing.Imagenes WHERE ImagenID = @Id", new { Id = id });
    }

    // ── Envío ─────────────────────────────────────────────────────────────────

    public async Task<string> EnviarPruebaAsync(int campanaId, string emailDestino)
    {
        var campana = await ObtenerCampanaAsync(campanaId)
            ?? throw new KeyNotFoundException("Campaña no encontrada.");

        var empresa = await ObtenerNombreEmpresaAsync();
        var html    = CampanaEmailBuilder.Preview(campana.BloqueJSON, empresa, "Destinatario de Prueba");

        var ok = await email.SendAsync(new EmailMessage(emailDestino,
            $"[PRUEBA] {campana.Asunto}", html, "Destinatario de Prueba"));

        return ok ? $"Email de prueba enviado a {emailDestino}."
                  : "El email no pudo enviarse. Verifica la configuración en Settings → Email.";
    }

    public async Task IniciarEnvioAsync(int campanaId, string apiBaseUrl)
    {
        var campana = await ObtenerCampanaAsync(campanaId)
            ?? throw new KeyNotFoundException("Campaña no encontrada.");

        if (campana.Estado != "BORRADOR")
            throw new InvalidOperationException("Solo se pueden enviar campañas en estado BORRADOR.");

        var destinatarios = (await ObtenerDestinatariosAsync(campana.SegmentoTipo, campana.SegmentoValor)).ToList();

        using (var c = db.CreateConnection())
        {
            await c.ExecuteAsync(@"
                UPDATE Marketing.Campanas
                SET Estado = 'ENVIANDO', TotalDestinatarios = @Total, FechaEnvio = SYSUTCDATETIME()
                WHERE CampanaID = @Id",
                new { Id = campanaId, Total = destinatarios.Count });

            foreach (var (clienteId, correo, nombre, empresa) in destinatarios)
            {
                var token = Guid.NewGuid().ToString("N");
                await c.ExecuteAsync(@"
                    INSERT INTO Marketing.EnviosDetalle (CampanaID, ClienteID, Email, NombreCliente, EmpresaCliente, Token)
                    VALUES (@CampanaID, @ClienteID, @Email, @Nombre, @Empresa, @Token)",
                    new { CampanaID = campanaId, ClienteID = clienteId, Email = correo, Nombre = nombre, Empresa = empresa, Token = token });
            }
        }

        // Envío en segundo plano — no bloquea el request
        _ = Task.Run(() => EnviarEnSegundoPlanoAsync(campanaId, campana, apiBaseUrl));
    }

    private async Task EnviarEnSegundoPlanoAsync(int campanaId, CampanaDetalle campana, string apiBaseUrl)
    {
        var empresa = await ObtenerNombreEmpresaAsync();

        using var c = db.CreateConnection();
        var envios = (await c.QueryAsync<(long EnvioID, string Email, string Nombre, string? Empresa, string Token)>(@"
            SELECT EnvioID, Email, NombreCliente, EmpresaCliente, Token
            FROM Marketing.EnviosDetalle
            WHERE CampanaID = @Id AND Enviado = 0",
            new { Id = campanaId })).ToList();

        int enviados = 0;
        foreach (var e in envios)
        {
            try
            {
                var pixelUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
                    ? null : $"{apiBaseUrl}/api/marketing/track/open/{e.Token}";
                var unsubUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
                    ? null : $"{apiBaseUrl}/api/marketing/unsub/{e.Token}";

                var asunto = campana.Asunto
                    .Replace("{{nombre}}", e.Nombre)
                    .Replace("{{empresa}}", e.Empresa ?? "");

                var html = CampanaEmailBuilder.Build(
                    campana.BloqueJSON, empresa, "#7c5cfc",
                    e.Nombre, e.Empresa, pixelUrl, unsubUrl);

                var ok = await email.SendAsync(new EmailMessage(e.Email, asunto, html, e.Nombre));

                await c.ExecuteAsync(@"
                    UPDATE Marketing.EnviosDetalle
                    SET Enviado = @Ok, FechaEnvio = SYSUTCDATETIME(),
                        Error = @Error
                    WHERE EnvioID = @EnvioID",
                    new { Ok = ok, Error = ok ? (string?)null : "Fallo al enviar", e.EnvioID });

                if (ok) enviados++;

                // Registrar como interacción CRM
                if (ok)
                    await c.ExecuteAsync(@"
                        INSERT INTO Crm.Interacciones (ClienteID, Tipo, Notas, Fecha, UsuarioID)
                        SELECT cl.ClienteID, 'EMAIL',
                               CONCAT('Campaña enviada: ', @Asunto),
                               SYSUTCDATETIME(), NULL
                        FROM Marketing.EnviosDetalle ed
                        JOIN Crm.Clientes cl ON cl.ClienteID = ed.ClienteID
                        WHERE ed.EnvioID = @EnvioID",
                        new { Asunto = campana.Asunto, e.EnvioID });

                // Pequeña pausa para no saturar Gmail (máx ~1/seg)
                await Task.Delay(1200);
            }
            catch { /* continuar con el siguiente */ }
        }

        using var c2 = db.CreateConnection();
        await c2.ExecuteAsync(@"
            UPDATE Marketing.Campanas
            SET Estado = 'ENVIADA', TotalEnviados = @Enviados
            WHERE CampanaID = @Id",
            new { Id = campanaId, Enviados = enviados });
    }

    // ── Tracking ──────────────────────────────────────────────────────────────

    public async Task<bool> RegistrarAperturaAsync(string token)
    {
        using var c = db.CreateConnection();
        var envio = await c.QueryFirstOrDefaultAsync<(long EnvioID, int CampanaID, bool Abierto)>(
            "SELECT EnvioID, CampanaID, Abierto FROM Marketing.EnviosDetalle WHERE Token = @Token",
            new { Token = token });
        if (envio == default || envio.Abierto) return false;

        await c.ExecuteAsync(@"
            UPDATE Marketing.EnviosDetalle SET Abierto = 1, FechaApertura = SYSUTCDATETIME() WHERE EnvioID = @Id;
            UPDATE Marketing.Campanas SET TotalAbiertos = TotalAbiertos + 1 WHERE CampanaID = @CampanaID;",
            new { Id = envio.EnvioID, CampanaID = envio.CampanaID });

        // Registrar apertura como interacción CRM
        await c.ExecuteAsync(@"
            INSERT INTO Crm.Interacciones (ClienteID, Tipo, Notas, Fecha, UsuarioID)
            SELECT ed.ClienteID, 'EMAIL', CONCAT('Abrió campaña: ', ca.Asunto), SYSUTCDATETIME(), NULL
            FROM Marketing.EnviosDetalle ed
            JOIN Marketing.Campanas ca ON ca.CampanaID = ed.CampanaID
            WHERE ed.EnvioID = @Id",
            new { Id = envio.EnvioID });

        return true;
    }

    public async Task<string?> RegistrarClickAsync(string token, string url)
    {
        using var c = db.CreateConnection();
        var envio = await c.QueryFirstOrDefaultAsync<(long EnvioID, int CampanaID)>(
            "SELECT EnvioID, CampanaID FROM Marketing.EnviosDetalle WHERE Token = @Token",
            new { Token = token });
        if (envio == default) return url;

        await c.ExecuteAsync(@"
            UPDATE Marketing.EnviosDetalle
            SET Clicks = Clicks + 1, UltimoClick = SYSUTCDATETIME() WHERE EnvioID = @Id;
            UPDATE Marketing.Campanas SET TotalClicks = TotalClicks + 1 WHERE CampanaID = @CampanaID;",
            new { Id = envio.EnvioID, CampanaID = envio.CampanaID });

        return url;
    }

    public async Task<bool> RegistrarDesuscripcionAsync(string token)
    {
        using var c = db.CreateConnection();
        var filas = await c.ExecuteAsync(@"
            UPDATE Marketing.EnviosDetalle
            SET Desuscripto = 1, FechaDesuscripcion = SYSUTCDATETIME() WHERE Token = @Token AND Desuscripto = 0;
            UPDATE Marketing.Campanas
            SET TotalDesuscriptos = TotalDesuscriptos + 1
            WHERE CampanaID = (SELECT CampanaID FROM Marketing.EnviosDetalle WHERE Token = @Token);",
            new { Token = token });
        return filas > 0;
    }

    public async Task<IEnumerable<EnvioDetalleItem>> ListarEnviosDetalleAsync(int campanaId)
    {
        using var c = db.CreateConnection();
        return await c.QueryAsync<EnvioDetalleItem>(@"
            SELECT EnvioID, ClienteID, NombreCliente, Email,
                   Enviado, FechaEnvio, Abierto, FechaApertura,
                   Clicks, Desuscripto, Error
            FROM Marketing.EnviosDetalle
            WHERE CampanaID = @Id ORDER BY EnvioID",
            new { Id = campanaId });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public async Task<string> ObtenerNombreEmpresaAsync()
    {
        using var c = db.CreateConnection();
        return await c.ExecuteScalarAsync<string>(
            "SELECT TOP 1 NombreEmpresa FROM Organizacion.ConfiguracionEmpresa") ?? "NEXO ERP";
    }
}
