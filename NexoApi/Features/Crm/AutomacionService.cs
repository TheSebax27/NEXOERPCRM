using System.Data;
using System.Text.Json;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Crm.Dtos;
using NexoApi.Features.Email;

namespace NexoApi.Features.Crm;

public interface IAutomacionService
{
    Task<IEnumerable<ReglaAutomacionItem>> ListarReglasAsync();
    Task ActualizarReglaAsync(int reglaId, ActualizarReglaRequest request);
    Task EvaluarReglasPeriodicasAsync();
    Task EvaluarEtapaCambiadaAsync(int oportunidadId, int? clienteId, int? responsableId, string etapaNueva);
    Task<int> EjecutarReglaAhoraAsync(int reglaId);
}

public class AutomacionService : IAutomacionService
{
    private readonly IDbConnectionFactory _db;
    private readonly IEmailService _email;

    public AutomacionService(IDbConnectionFactory db, IEmailService email) { _db = db; _email = email; }

    // ── CRUD Reglas ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<ReglaAutomacionItem>> ListarReglasAsync()
    {
        using var con = _db.CreateConnection();
        return await con.QueryAsync<ReglaAutomacionItem>(
            "SELECT ReglaID, Nombre, Descripcion, Evento, ParametrosJSON, Activa, FechaCreacion FROM Crm.ReglasAutomatizacion ORDER BY ReglaID");
    }

    public async Task ActualizarReglaAsync(int reglaId, ActualizarReglaRequest r)
    {
        using var con = _db.CreateConnection();
        await con.ExecuteAsync(@"
            UPDATE Crm.ReglasAutomatizacion
            SET Activa = @Activa, ParametrosJSON = @ParametrosJSON
            WHERE ReglaID = @ReglaID",
            new { ReglaID = reglaId, r.Activa, r.ParametrosJSON });
    }

    // ── Ejecución manual de una regla ────────────────────────────────────────

    public async Task<int> EjecutarReglaAhoraAsync(int reglaId)
    {
        using var con = _db.CreateConnection();
        var regla = await con.QueryFirstOrDefaultAsync<ReglaRow>(
            "SELECT ReglaID,Nombre,Descripcion,Evento,ParametrosJSON,Activa,FechaCreacion FROM Crm.ReglasAutomatizacion WHERE ReglaID = @ReglaID", new { ReglaID = reglaId })
            ?? throw new KeyNotFoundException($"Regla {reglaId} no encontrada.");

        return await EvaluarReglaAsync(con, regla, ignorarDedup: true);
    }

    // ── Evaluación periódica (llamada desde Background Service) ───────────────

    public async Task EvaluarReglasPeriodicasAsync()
    {
        using var con = _db.CreateConnection();
        var reglas = (await con.QueryAsync<ReglaRow>(
            "SELECT ReglaID,Nombre,Descripcion,Evento,ParametrosJSON,Activa,FechaCreacion FROM Crm.ReglasAutomatizacion WHERE Activa = 1 AND Evento <> 'ETAPA_CAMBIADA'")).ToList();

        foreach (var r in reglas)
            await EvaluarReglaAsync(con, r, ignorarDedup: false);
    }

    // ── Hook para cambios de etapa (llamado desde CrmService) ────────────────

    public async Task EvaluarEtapaCambiadaAsync(int oportunidadId, int? clienteId, int? responsableId, string etapaNueva)
    {
        using var con = _db.CreateConnection();
        var reglas = await con.QueryAsync<ReglaRow>(
            "SELECT ReglaID,Nombre,Descripcion,Evento,ParametrosJSON,Activa,FechaCreacion FROM Crm.ReglasAutomatizacion WHERE Activa = 1 AND Evento = 'ETAPA_CAMBIADA'");

        foreach (var r in reglas)
        {
            var p = Params(r);
            var etapaRequerida = Str(p, "EtapaDestino");
            if (etapaRequerida is not null && etapaRequerida != etapaNueva) continue;
            if (await YaEjecutadaHoyAsync(con, r.ReglaID, "Oportunidad", oportunidadId)) continue;
            await CrearActividadAsync(con, r, p, "Oportunidad", oportunidadId, oportunidadId, clienteId, responsableId);
        }
    }

    // ── Lógica interna ───────────────────────────────────────────────────────

    private async Task<int> EvaluarReglaAsync(IDbConnection con, ReglaRow r, bool ignorarDedup)
    {
        int creadas = 0;
        switch (r.Evento)
        {
            case "COTIZACION_PENDIENTE":     creadas = await EvalCotizacionPendienteAsync(con, r, ignorarDedup); break;
            case "COTIZACION_POR_VENCER":    creadas = await EvalCotizacionPorVencerAsync(con, r, ignorarDedup); break;
            case "CLIENTE_FRIO":             creadas = await EvalClienteFrioAsync(con, r, ignorarDedup); break;
            case "OPORTUNIDAD_VENCIDA":      creadas = await EvalOportunidadVencidaAsync(con, r, ignorarDedup); break;
            case "OPORTUNIDAD_SIN_ACTIVIDAD": creadas = await EvalOportunidadSinActividadAsync(con, r, ignorarDedup); break;
        }
        return creadas;
    }

    private async Task<int> EvalCotizacionPorVencerAsync(IDbConnection con, ReglaRow r, bool ignorarDedup)
    {
        var p = Params(r);
        var dias = Int(p, "DiasAntes", 2);

        var entidades = await con.QueryAsync<(int ID, int ClienteID, string Cliente, string? Email, DateTime ValidoHasta, decimal Total)>(@"
            SELECT c.CotizacionID AS ID, c.ClienteID, cl.Nombre AS Cliente, cl.Email,
                   c.ValidoHasta,
                   ISNULL((SELECT SUM(l.Cantidad * l.PrecioUnitario)
                           FROM Crm.CotizacionLineas l WHERE l.CotizacionID = c.CotizacionID), 0) AS Total
            FROM Crm.Cotizaciones c
            JOIN Crm.Clientes cl ON cl.ClienteID = c.ClienteID
            WHERE c.Estado NOT IN ('ACEPTADA','RECHAZADA')
              AND c.ValidoHasta IS NOT NULL
              AND DATEDIFF(DAY, SYSUTCDATETIME(), c.ValidoHasta) BETWEEN 0 AND @Dias",
            new { Dias = dias });

        var empresa = await con.ExecuteScalarAsync<string>(
            "SELECT TOP 1 NombreEmpresa FROM Organizacion.ConfiguracionEmpresa") ?? "NEXO ERP";

        int cnt = 0;
        foreach (var e in entidades)
        {
            if (!ignorarDedup && await YaEjecutadaHoyAsync(con, r.ReglaID, "Cotizacion", e.ID)) continue;

            // Crear actividad interna
            await CrearActividadAsync(con, r, p, "Cotizacion", e.ID, null, e.ClienteID, null);

            // Enviar email al cliente si tiene email
            if (!string.IsNullOrWhiteSpace(e.Email))
            {
                int diasRestantes = (int)(e.ValidoHasta.Date - DateTime.UtcNow.Date).TotalDays;
                var html = EmailTemplates.CotizacionPorVencer(empresa, e.Cliente, e.ID, e.ValidoHasta, e.Total, diasRestantes);
                await _email.SendAsync(new EmailMessage(e.Email, $"Recordatorio: tu cotización #{e.ID} por vencer", html, e.Cliente));
            }

            cnt++;
        }
        return cnt;
    }

    private async Task<int> EvalCotizacionPendienteAsync(IDbConnection con, ReglaRow r, bool ignorarDedup)
    {
        var p = Params(r);
        var dias = Int(p, "DiasEspera", 3);
        var entidades = await con.QueryAsync<(int ID, int ClienteID)>(@"
            SELECT c.CotizacionID AS ID, c.ClienteID
            FROM Crm.Cotizaciones c
            WHERE c.Estado = 'ENVIADA'
              AND DATEDIFF(DAY, c.Fecha, SYSUTCDATETIME()) >= @Dias",
            new { Dias = dias });

        int cnt = 0;
        foreach (var (id, clienteId) in entidades)
        {
            if (!ignorarDedup && await YaEjecutadaHoyAsync(con, r.ReglaID, "Cotizacion", id)) continue;
            await CrearActividadAsync(con, r, p, "Cotizacion", id, null, clienteId, null);
            cnt++;
        }
        return cnt;
    }

    private async Task<int> EvalClienteFrioAsync(IDbConnection con, ReglaRow r, bool ignorarDedup)
    {
        var p = Params(r);
        var dias = Int(p, "DiasSinContacto", 60);
        var entidades = await con.QueryAsync<(int ClienteID, int? ResponsableID)>(@"
            SELECT c.ClienteID,
                   CASE WHEN u.UsuarioID IS NOT NULL THEN c.ResponsableID ELSE NULL END AS ResponsableID
            FROM Crm.Clientes c
            LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = c.ResponsableID
            WHERE c.Estado = 1
              AND ISNULL(
                (SELECT MAX(i.Fecha) FROM Crm.Interacciones i WHERE i.ClienteID = c.ClienteID),
                '2000-01-01'
              ) < DATEADD(DAY, -@Dias, SYSUTCDATETIME())",
            new { Dias = dias });

        int cnt = 0;
        foreach (var (clienteId, responsableId) in entidades)
        {
            if (!ignorarDedup && await YaEjecutadaHoyAsync(con, r.ReglaID, "Cliente", clienteId)) continue;
            await CrearActividadAsync(con, r, p, "Cliente", clienteId, null, clienteId, responsableId);
            cnt++;
        }
        return cnt;
    }

    private async Task<int> EvalOportunidadVencidaAsync(IDbConnection con, ReglaRow r, bool ignorarDedup)
    {
        var p = Params(r);
        // Filtro de confianza mínima: OPTIMISTA > NEUTRO > BAJA
        var confianzaMin = Str(p, "ConfianzaMinima") ?? "BAJA";
        var confianzasFiltro = confianzaMin switch
        {
            "OPTIMISTA" => new[] { "OPTIMISTA" },
            "NEUTRO"    => new[] { "OPTIMISTA", "NEUTRO" },
            _           => new[] { "OPTIMISTA", "NEUTRO", "BAJA" }
        };
        var entidades = await con.QueryAsync<(int OportunidadID, int? ClienteID, int? ResponsableID)>(@"
            SELECT OportunidadID, ClienteID, ResponsableID
            FROM Crm.Oportunidades
            WHERE Etapa NOT IN ('GANADA','PERDIDA')
              AND FechaCierreEsperada IS NOT NULL
              AND FechaCierreEsperada < SYSUTCDATETIME()
              AND ConfianzaCierre IN @Confianzas",
            new { Confianzas = confianzasFiltro });

        int cnt = 0;
        foreach (var (opId, clienteId, respId) in entidades)
        {
            if (!ignorarDedup && await YaEjecutadaHoyAsync(con, r.ReglaID, "Oportunidad", opId)) continue;
            await CrearActividadAsync(con, r, p, "Oportunidad", opId, opId, clienteId, respId);
            cnt++;
        }
        return cnt;
    }

    private async Task<int> EvalOportunidadSinActividadAsync(IDbConnection con, ReglaRow r, bool ignorarDedup)
    {
        var p = Params(r);
        var dias = Int(p, "DiasMaxSinActividad", 14);
        var entidades = await con.QueryAsync<(int OportunidadID, int? ClienteID, int? ResponsableID)>(@"
            SELECT OportunidadID, ClienteID, ResponsableID
            FROM Crm.Oportunidades
            WHERE Etapa NOT IN ('GANADA','PERDIDA')
              AND ISNULL(
                (SELECT MAX(a.FechaCreacion) FROM Crm.Actividades a WHERE a.OportunidadID = Crm.Oportunidades.OportunidadID),
                '2000-01-01'
              ) < DATEADD(DAY, -@Dias, SYSUTCDATETIME())",
            new { Dias = dias });

        int cnt = 0;
        foreach (var (opId, clienteId, respId) in entidades)
        {
            if (!ignorarDedup && await YaEjecutadaHoyAsync(con, r.ReglaID, "Oportunidad", opId)) continue;
            await CrearActividadAsync(con, r, p, "Oportunidad", opId, opId, clienteId, respId);
            cnt++;
        }
        return cnt;
    }

    private async Task<bool> YaEjecutadaHoyAsync(IDbConnection con, int reglaId, string tipo, int id)
        => await con.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM Crm.EjecucionesAutomatizacion
            WHERE ReglaID = @ReglaID AND EntidadTipo = @Tipo AND EntidadID = @ID
              AND FechaEjecucion >= DATEADD(HOUR, -24, SYSUTCDATETIME())",
            new { ReglaID = reglaId, Tipo = tipo, ID = id }) > 0;

    private async Task CrearActividadAsync(IDbConnection con, ReglaRow r, JsonElement p,
        string entidadTipo, int entidadId,
        int? oportunidadId, int? clienteId, int? responsableId)
    {
        var tipo   = Str(p, "TipoActividad") ?? "TAREA";
        var titulo = Str(p, "TituloActividad") ?? r.Nombre;
        var dias   = Int(p, "DiasVencimiento", 1);
        var vence  = DateTime.UtcNow.AddDays(dias);

        await con.ExecuteAsync(@"
            INSERT INTO Crm.Actividades (Tipo, Titulo, Notas, FechaVencimiento, OportunidadID, ClienteID, ResponsableID)
            VALUES (@Tipo, @Titulo, @Notas, @Vence, @OpID, @ClID, @RespID)",
            new { Tipo = tipo, Titulo = titulo, Notas = $"Creada automáticamente por la regla «{r.Nombre}».",
                  Vence = vence, OpID = oportunidadId, ClID = clienteId, RespID = responsableId });

        await con.ExecuteAsync(@"
            INSERT INTO Crm.EjecucionesAutomatizacion (ReglaID, EntidadTipo, EntidadID)
            VALUES (@ReglaID, @Tipo, @ID)",
            new { ReglaID = r.ReglaID, Tipo = entidadTipo, ID = entidadId });
    }

    // ── Helpers JSON ─────────────────────────────────────────────────────────

    private static JsonElement Params(ReglaRow r)
    {
        try { return JsonDocument.Parse(r.ParametrosJSON).RootElement; }
        catch { return JsonDocument.Parse("{}").RootElement; }
    }

    private static string? Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) ? v.GetString() : null;

    private static int Int(JsonElement e, string key, int def)
        => e.TryGetProperty(key, out var v) && v.TryGetInt32(out var i) ? i : def;

    private record ReglaRow(int ReglaID, string Nombre, string? Descripcion, string Evento, string ParametrosJSON, bool Activa, DateTime FechaCreacion);
}
