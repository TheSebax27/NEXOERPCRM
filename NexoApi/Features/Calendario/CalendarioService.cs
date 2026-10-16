using System.Data;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Calendario.Dtos;

namespace NexoApi.Features.Calendario;

public interface ICalendarioService
{
    Task<IEnumerable<EventoCalendarioItem>> ListarEventosAsync(DateTime desde, DateTime hasta, int? soloParaUsuarioId);
    Task<EventoCalendarioItem?> ObtenerProximoEventoAsync(int usuarioId, bool esAdmin);
    Task<IEnumerable<UsuarioDisponibleItem>> ListarUsuariosDisponiblesAsync();
    Task<int> CrearEventoAsync(CrearEventoRequest request, int creadorId);
    Task ActualizarEventoAsync(int eventoId, ActualizarEventoRequest request);
    Task EliminarEventoAsync(int eventoId);
}

public class CalendarioService : ICalendarioService
{
    private readonly IDbConnectionFactory _db;

    public CalendarioService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<EventoCalendarioItem>> ListarEventosAsync(DateTime desde, DateTime hasta, int? soloParaUsuarioId)
    {
        using var connection = _db.CreateConnection();

        List<EventoCalendarioRow> eventos;

        if (soloParaUsuarioId.HasValue)
        {
            const string sql = @"
                SELECT DISTINCT e.EventoID, e.Titulo, e.TipoEvento,
                       e.FechaInicio, e.FechaFin, e.Lugar, e.LinkVirtual, e.Descripcion,
                       e.CentroCostoID, cc.Nombre AS CentroCosto,
                       e.CreadoPorUsuarioID, u.Nombres + ' ' + u.Apellidos AS CreadoPor
                FROM Calendario.Eventos e
                LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = e.CentroCostoID
                LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = e.CreadoPorUsuarioID
                LEFT JOIN Calendario.EventoAsistentes ea ON ea.EventoID = e.EventoID AND ea.UsuarioID = @UsuarioID
                WHERE e.Activo = 1
                  AND e.FechaInicio < @Hasta
                  AND e.FechaFin >= @Desde
                  AND (e.CreadoPorUsuarioID = @UsuarioID OR ea.UsuarioID IS NOT NULL)
                ORDER BY e.FechaInicio";
            eventos = (await connection.QueryAsync<EventoCalendarioRow>(sql,
                new { Desde = desde, Hasta = hasta, UsuarioID = soloParaUsuarioId.Value })).ToList();
        }
        else
        {
            const string sql = @"
                SELECT e.EventoID, e.Titulo, e.TipoEvento,
                       e.FechaInicio, e.FechaFin, e.Lugar, e.LinkVirtual, e.Descripcion,
                       e.CentroCostoID, cc.Nombre AS CentroCosto,
                       e.CreadoPorUsuarioID, u.Nombres + ' ' + u.Apellidos AS CreadoPor
                FROM Calendario.Eventos e
                LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = e.CentroCostoID
                LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = e.CreadoPorUsuarioID
                WHERE e.Activo = 1
                  AND e.FechaInicio < @Hasta
                  AND e.FechaFin >= @Desde
                ORDER BY e.FechaInicio";
            eventos = (await connection.QueryAsync<EventoCalendarioRow>(sql,
                new { Desde = desde, Hasta = hasta })).ToList();
        }

        if (eventos.Count == 0) return [];

        var asistentesPorEvento = await CargarAsistentesAsync(connection, eventos.Select(e => e.EventoID));

        return eventos.Select(e => MapearEvento(e, asistentesPorEvento));
    }

    public async Task<EventoCalendarioItem?> ObtenerProximoEventoAsync(int usuarioId, bool esAdmin)
    {
        using var connection = _db.CreateConnection();

        EventoCalendarioRow? row;

        if (esAdmin)
        {
            const string sql = @"
                SELECT TOP 1 e.EventoID, e.Titulo, e.TipoEvento,
                       e.FechaInicio, e.FechaFin, e.Lugar, e.LinkVirtual, e.Descripcion,
                       e.CentroCostoID, cc.Nombre AS CentroCosto,
                       e.CreadoPorUsuarioID, u.Nombres + ' ' + u.Apellidos AS CreadoPor
                FROM Calendario.Eventos e
                LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = e.CentroCostoID
                LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = e.CreadoPorUsuarioID
                WHERE e.Activo = 1 AND e.FechaFin >= GETUTCDATE()
                ORDER BY e.FechaInicio ASC";
            row = await connection.QueryFirstOrDefaultAsync<EventoCalendarioRow>(sql);
        }
        else
        {
            const string sql = @"
                SELECT TOP 1 e.EventoID, e.Titulo, e.TipoEvento,
                       e.FechaInicio, e.FechaFin, e.Lugar, e.LinkVirtual, e.Descripcion,
                       e.CentroCostoID, cc.Nombre AS CentroCosto,
                       e.CreadoPorUsuarioID, u.Nombres + ' ' + u.Apellidos AS CreadoPor
                FROM Calendario.Eventos e
                LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = e.CentroCostoID
                LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = e.CreadoPorUsuarioID
                LEFT JOIN Calendario.EventoAsistentes ea ON ea.EventoID = e.EventoID AND ea.UsuarioID = @UsuarioID
                WHERE e.Activo = 1 AND e.FechaFin >= GETUTCDATE()
                  AND (e.CreadoPorUsuarioID = @UsuarioID OR ea.UsuarioID IS NOT NULL)
                ORDER BY e.FechaInicio ASC";
            row = await connection.QueryFirstOrDefaultAsync<EventoCalendarioRow>(sql, new { UsuarioID = usuarioId });
        }

        if (row is null) return null;

        var asistentesPorEvento = await CargarAsistentesAsync(connection, [row.EventoID]);
        return MapearEvento(row, asistentesPorEvento);
    }

    public async Task<IEnumerable<UsuarioDisponibleItem>> ListarUsuariosDisponiblesAsync()
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT u.UsuarioID, u.Nombres + ' ' + u.Apellidos AS Nombre, u.Email
            FROM Seguridad.Usuarios u
            WHERE u.Estado = 1
            ORDER BY u.Nombres, u.Apellidos";
        return await connection.QueryAsync<UsuarioDisponibleItem>(sql);
    }

    public async Task<int> CrearEventoAsync(CrearEventoRequest request, int creadorId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Calendario.Eventos
                (Titulo, TipoEvento, FechaInicio, FechaFin, Lugar, LinkVirtual, Descripcion, CentroCostoID, CreadoPorUsuarioID)
            VALUES
                (@Titulo, @TipoEvento, @FechaInicio, @FechaFin, @Lugar, @LinkVirtual, @Descripcion, @CentroCostoID, @CreadoPorUsuarioID);
            SELECT CAST(SCOPE_IDENTITY() AS INT)";

        var eventoId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            request.Titulo, request.TipoEvento, request.FechaInicio, request.FechaFin,
            request.Lugar, request.LinkVirtual, request.Descripcion, request.CentroCostoID,
            CreadoPorUsuarioID = creadorId
        });

        await InsertarAsistentesAsync(connection, eventoId, request.AsistenteIds);
        return eventoId;
    }

    public async Task ActualizarEventoAsync(int eventoId, ActualizarEventoRequest request)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            UPDATE Calendario.Eventos
            SET Titulo = @Titulo, TipoEvento = @TipoEvento,
                FechaInicio = @FechaInicio, FechaFin = @FechaFin,
                Lugar = @Lugar, LinkVirtual = @LinkVirtual,
                Descripcion = @Descripcion, CentroCostoID = @CentroCostoID
            WHERE EventoID = @EventoID AND Activo = 1";

        await connection.ExecuteAsync(sql, new
        {
            EventoID = eventoId,
            request.Titulo, request.TipoEvento, request.FechaInicio, request.FechaFin,
            request.Lugar, request.LinkVirtual, request.Descripcion, request.CentroCostoID
        });

        await connection.ExecuteAsync(
            "DELETE FROM Calendario.EventoAsistentes WHERE EventoID = @EventoID",
            new { EventoID = eventoId });

        await InsertarAsistentesAsync(connection, eventoId, request.AsistenteIds);
    }

    public async Task EliminarEventoAsync(int eventoId)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Calendario.Eventos SET Activo = 0 WHERE EventoID = @EventoID",
            new { EventoID = eventoId });
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<Dictionary<int, List<AsistenteEventoItem>>> CargarAsistentesAsync(
        IDbConnection connection, IEnumerable<int> eventoIds)
    {
        var ids = eventoIds.ToList();
        if (ids.Count == 0) return [];

        const string sql = @"
            SELECT ea.EventoID, ea.UsuarioID, u.Nombres + ' ' + u.Apellidos AS Nombre, ea.Estado
            FROM Calendario.EventoAsistentes ea
            JOIN Seguridad.Usuarios u ON u.UsuarioID = ea.UsuarioID
            WHERE ea.EventoID IN @Ids";

        var rows = await connection.QueryAsync<AsistenteRow>(sql, new { Ids = ids });

        return rows.GroupBy(r => r.EventoID)
            .ToDictionary(g => g.Key,
                g => g.Select(r => new AsistenteEventoItem(r.UsuarioID, r.Nombre, r.Estado)).ToList());
    }

    private static EventoCalendarioItem MapearEvento(
        EventoCalendarioRow e, Dictionary<int, List<AsistenteEventoItem>> asistentesPorEvento)
    {
        asistentesPorEvento.TryGetValue(e.EventoID, out var asistentes);
        return new EventoCalendarioItem(
            e.EventoID, e.Titulo, e.TipoEvento,
            e.FechaInicio, e.FechaFin,
            e.Lugar, e.LinkVirtual, e.Descripcion,
            e.CentroCostoID, e.CentroCosto,
            e.CreadoPorUsuarioID, e.CreadoPor,
            asistentes ?? []);
    }

    private static async Task InsertarAsistentesAsync(
        IDbConnection connection, int eventoId, List<int> usuarioIds)
    {
        if (usuarioIds.Count == 0) return;

        const string sql = @"
            INSERT INTO Calendario.EventoAsistentes (EventoID, UsuarioID, Estado)
            VALUES (@EventoID, @UsuarioID, 'Pendiente')";

        await connection.ExecuteAsync(sql,
            usuarioIds.Select(uid => new { EventoID = eventoId, UsuarioID = uid }));
    }
}
