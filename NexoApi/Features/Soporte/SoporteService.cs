using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Soporte.Dtos;

namespace NexoApi.Features.Soporte;

public interface ISoporteService
{
    Task<IEnumerable<TicketItem>> ListarTicketsAsync(string? estado, string? prioridad, int? asignadoA, int? reportadoPor, int? clienteId = null);
    Task<TicketItem?> ObtenerTicketAsync(int ticketId);
    Task<int> CrearTicketAsync(CrearTicketRequest request, int usuarioId);
    Task ActualizarTicketAsync(int ticketId, ActualizarTicketRequest request);
    Task CambiarEstadoAsync(int ticketId, string estado, int usuarioId);
    Task<IEnumerable<TicketComentarioItem>> ListarComentariosAsync(int ticketId);
    Task AgregarComentarioAsync(int ticketId, CrearTicketComentarioRequest request, int usuarioId);
    // NPS
    Task<NpsItem?> ObtenerNpsAsync(int ticketId);
    Task RegistrarNpsAsync(int ticketId, RegistrarNpsRequest request);
    Task<ResumenNpsItem> ObtenerResumenNpsAsync(DateTime desde, DateTime hasta);
}

public class SoporteService : ISoporteService
{
    private readonly IDbConnectionFactory _db;
    public SoporteService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<TicketItem>> ListarTicketsAsync(
        string? estado, string? prioridad, int? asignadoA, int? reportadoPor, int? clienteId = null)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT t.TicketID, t.Titulo, t.Descripcion, t.Categoria, t.Prioridad, t.Estado,
                   t.ReportadoPor,
                   ISNULL(ur.Nombres + ' ' + ur.Apellidos, '') AS ReportadoPorNombre,
                   t.AsignadoA,
                   ISNULL(ua.Nombres + ' ' + ua.Apellidos, NULL) AS AsignadoANombre,
                   t.ClienteID, c.Nombre AS Cliente, c.NIT AS NitCliente, c.Telefono AS TelefonoCliente,
                   t.FechaCreacion, t.FechaActualizacion, t.FechaResolucion, t.Notas,
                   (SELECT COUNT(*) FROM Soporte.TicketComentarios WHERE TicketID = t.TicketID) AS TotalComentarios
            FROM Soporte.Tickets t
            JOIN Seguridad.Usuarios ur ON ur.UsuarioID = t.ReportadoPor
            LEFT JOIN Seguridad.Usuarios ua ON ua.UsuarioID = t.AsignadoA
            LEFT JOIN Crm.Clientes c ON c.ClienteID = t.ClienteID
            WHERE (@Estado      IS NULL OR t.Estado    = @Estado)
              AND (@Prioridad   IS NULL OR t.Prioridad = @Prioridad)
              AND (@AsignadoA   IS NULL OR t.AsignadoA = @AsignadoA)
              AND (@ReportadoPor IS NULL OR t.ReportadoPor = @ReportadoPor)
              AND (@ClienteID   IS NULL OR t.ClienteID = @ClienteID)
            ORDER BY
                CASE t.Prioridad WHEN 'CRITICA' THEN 1 WHEN 'ALTA' THEN 2 WHEN 'MEDIA' THEN 3 ELSE 4 END,
                t.FechaCreacion DESC";

        return await con.QueryAsync<TicketItem>(sql,
            new { Estado = estado, Prioridad = prioridad, AsignadoA = asignadoA, ReportadoPor = reportadoPor, ClienteID = clienteId });
    }

    public async Task<TicketItem?> ObtenerTicketAsync(int ticketId)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT t.TicketID, t.Titulo, t.Descripcion, t.Categoria, t.Prioridad, t.Estado,
                   t.ReportadoPor,
                   ISNULL(ur.Nombres + ' ' + ur.Apellidos, '') AS ReportadoPorNombre,
                   t.AsignadoA,
                   ISNULL(ua.Nombres + ' ' + ua.Apellidos, NULL) AS AsignadoANombre,
                   t.ClienteID, c.Nombre AS Cliente,
                   t.FechaCreacion, t.FechaActualizacion, t.FechaResolucion, t.Notas,
                   (SELECT COUNT(*) FROM Soporte.TicketComentarios WHERE TicketID = t.TicketID) AS TotalComentarios
            FROM Soporte.Tickets t
            JOIN Seguridad.Usuarios ur ON ur.UsuarioID = t.ReportadoPor
            LEFT JOIN Seguridad.Usuarios ua ON ua.UsuarioID = t.AsignadoA
            LEFT JOIN Crm.Clientes c ON c.ClienteID = t.ClienteID
            WHERE t.TicketID = @TicketID";

        return await con.QueryFirstOrDefaultAsync<TicketItem>(sql, new { TicketID = ticketId });
    }

    public async Task<int> CrearTicketAsync(CrearTicketRequest r, int usuarioId)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            INSERT INTO Soporte.Tickets
                (Titulo, Descripcion, Categoria, Prioridad, Estado, ReportadoPor, AsignadoA, ClienteID, Notas)
            OUTPUT INSERTED.TicketID
            VALUES (@Titulo, @Descripcion, @Categoria, @Prioridad, 'ABIERTO', @UsuarioId, @AsignadoA, @ClienteID, @Notas)";

        return await con.ExecuteScalarAsync<int>(sql, new
        {
            r.Titulo, r.Descripcion, r.Categoria, r.Prioridad,
            UsuarioId = usuarioId, r.AsignadoA, r.ClienteID, r.Notas
        });
    }

    public async Task ActualizarTicketAsync(int ticketId, ActualizarTicketRequest r)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            UPDATE Soporte.Tickets
            SET Titulo = @Titulo, Descripcion = @Descripcion, Categoria = @Categoria,
                Prioridad = @Prioridad, Estado = @Estado,
                AsignadoA = @AsignadoA, ClienteID = @ClienteID, Notas = @Notas,
                FechaActualizacion = GETUTCDATE(),
                FechaResolucion = CASE WHEN @Estado IN ('RESUELTO','CERRADO') AND FechaResolucion IS NULL
                                       THEN GETUTCDATE() ELSE FechaResolucion END
            WHERE TicketID = @TicketID";

        var filas = await con.ExecuteAsync(sql, new
        {
            r.Titulo, r.Descripcion, r.Categoria, r.Prioridad, r.Estado,
            r.AsignadoA, r.ClienteID, r.Notas, TicketID = ticketId
        });
        if (filas == 0) throw new KeyNotFoundException($"Ticket {ticketId} no encontrado.");
    }

    public async Task CambiarEstadoAsync(int ticketId, string estado, int usuarioId)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            UPDATE Soporte.Tickets
            SET Estado = @Estado,
                FechaActualizacion = GETUTCDATE(),
                FechaResolucion = CASE WHEN @Estado IN ('RESUELTO','CERRADO') AND FechaResolucion IS NULL
                                       THEN GETUTCDATE() ELSE FechaResolucion END
            WHERE TicketID = @TicketID";

        await con.ExecuteAsync(sql, new { Estado = estado, TicketID = ticketId });
    }

    public async Task<IEnumerable<TicketComentarioItem>> ListarComentariosAsync(int ticketId)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT c.ComentarioID, c.TicketID, c.AutorID,
                   ISNULL(u.Nombres + ' ' + u.Apellidos, '') AS AutorNombre,
                   c.Texto, c.EsInterno, c.Fecha
            FROM Soporte.TicketComentarios c
            JOIN Seguridad.Usuarios u ON u.UsuarioID = c.AutorID
            WHERE c.TicketID = @TicketID
            ORDER BY c.Fecha ASC";

        return await con.QueryAsync<TicketComentarioItem>(sql, new { TicketID = ticketId });
    }

    public async Task AgregarComentarioAsync(int ticketId, CrearTicketComentarioRequest r, int usuarioId)
    {
        using var con = _db.CreateConnection();
        await con.ExecuteAsync(@"
            INSERT INTO Soporte.TicketComentarios (TicketID, AutorID, Texto, EsInterno)
            VALUES (@TicketID, @AutorID, @Texto, @EsInterno);
            UPDATE Soporte.Tickets SET FechaActualizacion = GETUTCDATE() WHERE TicketID = @TicketID",
            new { TicketID = ticketId, AutorID = usuarioId, r.Texto, r.EsInterno });
    }

    public async Task<NpsItem?> ObtenerNpsAsync(int ticketId)
    {
        using var con = _db.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<NpsItem>(
            "SELECT EncuestaNPSID, TicketID, Puntuacion, Comentario, FechaRespuesta FROM Soporte.EncuestasNPS WHERE TicketID = @TicketID",
            new { TicketID = ticketId });
    }

    public async Task RegistrarNpsAsync(int ticketId, RegistrarNpsRequest r)
    {
        using var con = _db.CreateConnection();
        await con.ExecuteAsync(@"
            IF EXISTS (SELECT 1 FROM Soporte.EncuestasNPS WHERE TicketID = @TicketID)
                UPDATE Soporte.EncuestasNPS SET Puntuacion = @Puntuacion, Comentario = @Comentario, FechaRespuesta = GETDATE()
                WHERE TicketID = @TicketID
            ELSE
                INSERT INTO Soporte.EncuestasNPS (TicketID, Puntuacion, Comentario)
                VALUES (@TicketID, @Puntuacion, @Comentario)",
            new { TicketID = ticketId, r.Puntuacion, r.Comentario });
    }

    public async Task<ResumenNpsItem> ObtenerResumenNpsAsync(DateTime desde, DateTime hasta)
    {
        using var con = _db.CreateConnection();
        var r = await con.QueryFirstOrDefaultAsync<(double Promedio, int Total, int Promotores, int Pasivos, int Detractores)>(@"
            SELECT
                ISNULL(AVG(CAST(Puntuacion AS FLOAT)), 0) AS Promedio,
                COUNT(*) AS Total,
                SUM(CASE WHEN Puntuacion >= 9 THEN 1 ELSE 0 END) AS Promotores,
                SUM(CASE WHEN Puntuacion BETWEEN 7 AND 8 THEN 1 ELSE 0 END) AS Pasivos,
                SUM(CASE WHEN Puntuacion <= 6 THEN 1 ELSE 0 END) AS Detractores
            FROM Soporte.EncuestasNPS
            WHERE FechaRespuesta BETWEEN @Desde AND @Hasta",
            new { Desde = desde, Hasta = hasta });
        return new ResumenNpsItem(r.Promedio, r.Total, r.Promotores, r.Pasivos, r.Detractores);
    }
}
