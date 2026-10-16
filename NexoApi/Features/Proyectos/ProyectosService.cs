using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Proyectos.Dtos;

namespace NexoApi.Features.Proyectos;

public interface IProyectosService
{
    Task<IEnumerable<ProyectoItem>> ListarProyectosAsync();
    Task<int> CrearProyectoAsync(CrearProyectoRequest request);
    Task ActualizarProyectoAsync(int proyectoId, ActualizarProyectoRequest request);

    Task<IEnumerable<TareaItem>> ListarTareasAsync(int proyectoId);
    Task<int> CrearTareaAsync(CrearTareaRequest request);
    Task ActualizarTareaAsync(int tareaId, ActualizarTareaRequest request);

    Task<IEnumerable<CostoItem>> ListarCostosAsync(int proyectoId);
    Task<int> CrearCostoAsync(CrearCostoRequest request, int usuarioId);

    Task<IEnumerable<HitoItem>> ListarHitosAsync(int proyectoId);
    Task<int> CrearHitoAsync(CrearHitoRequest request);
    Task ActualizarHitoAsync(int hitoId, ActualizarHitoRequest request);

    Task<IEnumerable<ProyectoDocumentoItem>> ListarDocumentosAsync(int proyectoId);
    Task<int> SubirDocumentoAsync(int proyectoId, SubirDocumentoProyectoRequest request, int usuarioId);
    Task<(byte[] Datos, string ContentType, string NombreArchivo)?> ObtenerDocumentoAsync(int documentoId);
    Task EliminarDocumentoAsync(int documentoId);

    Task<IEnumerable<ComentarioProyectoItem>> ListarComentariosAsync(int proyectoId);
    Task<int> CrearComentarioAsync(CrearComentarioRequest request, int usuarioId);

    Task<IEnumerable<ProyectoAlertaItem>> ListarAlertasAsync();
}

public class ProyectosService : IProyectosService
{
    private readonly IDbConnectionFactory _db;

    public ProyectosService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ProyectoItem>> ListarProyectosAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT p.ProyectoID, p.Nombre, p.ClienteID, c.Nombre AS Cliente,
                   p.CentroCostoID, cc.Nombre AS CentroCosto, p.Descripcion,
                   p.FechaInicio, p.FechaFin, p.Estado, p.Presupuesto, p.Prioridad,
                   ISNULL((SELECT SUM(co.Valor) FROM Proyectos.Costos co WHERE co.ProyectoID = p.ProyectoID), 0) AS CostoTotal,
                   (SELECT COUNT(*) FROM Proyectos.Tareas t WHERE t.ProyectoID = p.ProyectoID) AS TotalTareas,
                   (SELECT COUNT(*) FROM Proyectos.Tareas t WHERE t.ProyectoID = p.ProyectoID AND t.Estado = 'COMPLETADA') AS TareasCompletadas,
                   (SELECT COUNT(*) FROM Proyectos.Hitos h WHERE h.ProyectoID = p.ProyectoID) AS TotalHitos,
                   (SELECT COUNT(*) FROM Proyectos.Hitos h WHERE h.ProyectoID = p.ProyectoID AND h.Estado = 'COMPLETADO') AS HitosCompletados
            FROM Proyectos.Proyectos p
            LEFT JOIN Crm.Clientes c ON c.ClienteID = p.ClienteID
            LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = p.CentroCostoID
            ORDER BY p.FechaInicio DESC";

        return await connection.QueryAsync<ProyectoItem>(sql);
    }

    public async Task<int> CrearProyectoAsync(CrearProyectoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Proyectos.Proyectos (Nombre, ClienteID, CentroCostoID, Descripcion, FechaInicio, FechaFin, Presupuesto, Prioridad)
            OUTPUT INSERTED.ProyectoID
            VALUES (@Nombre, @ClienteID, @CentroCostoID, @Descripcion, @FechaInicio, @FechaFin, @Presupuesto, @Prioridad)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task ActualizarProyectoAsync(int proyectoId, ActualizarProyectoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            UPDATE Proyectos.Proyectos
            SET Nombre = @Nombre, ClienteID = @ClienteID, CentroCostoID = @CentroCostoID, Descripcion = @Descripcion,
                FechaInicio = @FechaInicio, FechaFin = @FechaFin, Estado = @Estado, Presupuesto = @Presupuesto, Prioridad = @Prioridad
            WHERE ProyectoID = @ProyectoId";

        var filas = await connection.ExecuteAsync(sql, new
        {
            ProyectoId = proyectoId,
            r.Nombre, r.ClienteID, r.CentroCostoID, r.Descripcion, r.FechaInicio, r.FechaFin, r.Estado, r.Presupuesto, r.Prioridad
        });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el proyecto {proyectoId}.");
    }

    private record DependenciaFila(int TareaID, int DependeDeTareaID);

    // Dapper no soporta mapear una fila directo a ValueTuple (ver leccion
    // 20.30 en CLAUDE.md) -- se usa un record privado en su lugar.
    private record TareaPlana(int TareaID, int ProyectoID, string Titulo, int? ResponsableID, string? Responsable, string Estado, DateTime? FechaLimite);

    public async Task<IEnumerable<TareaItem>> ListarTareasAsync(int proyectoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT t.TareaID, t.ProyectoID, t.Titulo, t.ResponsableID,
                   e.Nombres + ' ' + e.Apellidos AS Responsable, t.Estado, t.FechaLimite
            FROM Proyectos.Tareas t
            LEFT JOIN Rrhh.Empleados e ON e.EmpleadoID = t.ResponsableID
            WHERE t.ProyectoID = @ProyectoId
            ORDER BY t.FechaLimite ASC, t.TareaID";

        var tareas = (await connection.QueryAsync<TareaPlana>(sql, new { ProyectoId = proyectoId })).ToList();

        const string sqlDependencias = @"
            SELECT d.TareaID, d.DependeDeTareaID
            FROM Proyectos.TareaDependencias d
            JOIN Proyectos.Tareas t ON t.TareaID = d.TareaID
            WHERE t.ProyectoID = @ProyectoId";

        var dependencias = (await connection.QueryAsync<DependenciaFila>(sqlDependencias, new { ProyectoId = proyectoId }))
            .GroupBy(d => d.TareaID)
            .ToDictionary(g => g.Key, g => g.Select(d => d.DependeDeTareaID).ToList());

        return tareas.Select(t => new TareaItem(
            t.TareaID, t.ProyectoID, t.Titulo, t.ResponsableID, t.Responsable, t.Estado, t.FechaLimite,
            dependencias.TryGetValue(t.TareaID, out var deps) ? deps : new List<int>()));
    }

    public async Task<int> CrearTareaAsync(CrearTareaRequest r)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string sql = @"
                INSERT INTO Proyectos.Tareas (ProyectoID, Titulo, ResponsableID, FechaLimite)
                OUTPUT INSERTED.TareaID
                VALUES (@ProyectoID, @Titulo, @ResponsableID, @FechaLimite)";

            var id = await connection.ExecuteScalarAsync<int>(sql, new { r.ProyectoID, r.Titulo, r.ResponsableID, r.FechaLimite }, transaction);

            await GuardarDependenciasAsync(connection, transaction, id, r.DependeDeTareaIds);

            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private record TareaEstadoActual(string Estado);

    // Una tarea no puede pasar de PENDIENTE si alguna de las tareas de las
    // que depende todavia no esta COMPLETADA -- se valida aqui, no en el frontend.
    public async Task ActualizarTareaAsync(int tareaId, ActualizarTareaRequest r)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var actual = await connection.QuerySingleOrDefaultAsync<TareaEstadoActual>(
                "SELECT Estado FROM Proyectos.Tareas WHERE TareaID = @TareaId", new { TareaId = tareaId }, transaction);

            if (actual is null)
                throw new KeyNotFoundException($"No existe la tarea {tareaId}.");

            if (r.Estado != "PENDIENTE")
            {
                var dependeIds = r.DependeDeTareaIds ?? (await connection.QueryAsync<int>(
                    "SELECT DependeDeTareaID FROM Proyectos.TareaDependencias WHERE TareaID = @TareaId", new { TareaId = tareaId }, transaction)).ToList();

                if (dependeIds.Count > 0)
                {
                    var pendientes = await connection.QueryAsync<string>(
                        "SELECT Titulo FROM Proyectos.Tareas WHERE TareaID IN @Ids AND Estado <> 'COMPLETADA'",
                        new { Ids = dependeIds }, transaction);

                    var listaPendientes = pendientes.ToList();
                    if (listaPendientes.Count > 0)
                        throw new InvalidOperationException(
                            $"Esta tarea depende de otra(s) sin completar: {string.Join(", ", listaPendientes)}.");
                }
            }

            const string sql = @"
                UPDATE Proyectos.Tareas
                SET Titulo = @Titulo, ResponsableID = @ResponsableID, Estado = @Estado, FechaLimite = @FechaLimite
                WHERE TareaID = @TareaId";

            await connection.ExecuteAsync(sql, new { TareaId = tareaId, r.Titulo, r.ResponsableID, r.Estado, r.FechaLimite }, transaction);

            if (r.DependeDeTareaIds is not null)
                await GuardarDependenciasAsync(connection, transaction, tareaId, r.DependeDeTareaIds);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task GuardarDependenciasAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int tareaId, List<int>? dependeDeTareaIds)
    {
        await connection.ExecuteAsync("DELETE FROM Proyectos.TareaDependencias WHERE TareaID = @TareaId", new { TareaId = tareaId }, transaction);

        if (dependeDeTareaIds is null || dependeDeTareaIds.Count == 0)
            return;

        const string sql = "INSERT INTO Proyectos.TareaDependencias (TareaID, DependeDeTareaID) VALUES (@TareaId, @DependeDeTareaId)";
        foreach (var dependeDeId in dependeDeTareaIds.Distinct().Where(id => id != tareaId))
        {
            await connection.ExecuteAsync(sql, new { TareaId = tareaId, DependeDeTareaId = dependeDeId }, transaction);
        }
    }

    public async Task<IEnumerable<CostoItem>> ListarCostosAsync(int proyectoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT co.CostoID, co.ProyectoID, co.Tipo, co.Descripcion, co.Valor,
                   co.EmpleadoID, e.Nombres + ' ' + e.Apellidos AS Empleado,
                   co.ArticuloID, a.Nombre AS Articulo, co.Fecha
            FROM Proyectos.Costos co
            LEFT JOIN Rrhh.Empleados e ON e.EmpleadoID = co.EmpleadoID
            LEFT JOIN Catalogo.Tarjetas a ON a.ArticuloID = co.ArticuloID
            WHERE co.ProyectoID = @ProyectoId
            ORDER BY co.Fecha DESC";

        return await connection.QueryAsync<CostoItem>(sql, new { ProyectoId = proyectoId });
    }

    public async Task<int> CrearCostoAsync(CrearCostoRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Proyectos.Costos (ProyectoID, Tipo, Descripcion, Valor, EmpleadoID, ArticuloID, UsuarioID)
            OUTPUT INSERTED.CostoID
            VALUES (@ProyectoID, @Tipo, @Descripcion, @Valor, @EmpleadoID, @ArticuloID, @UsuarioID)";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            r.ProyectoID, r.Tipo, r.Descripcion, r.Valor, r.EmpleadoID, r.ArticuloID, UsuarioID = usuarioId
        });
    }

    // ---------- Hitos / Fases ----------

    public async Task<IEnumerable<HitoItem>> ListarHitosAsync(int proyectoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT HitoID, ProyectoID, Nombre, FechaObjetivo, FechaCompletado, Estado, Orden
            FROM Proyectos.Hitos
            WHERE ProyectoID = @ProyectoId
            ORDER BY Orden, FechaObjetivo";

        return await connection.QueryAsync<HitoItem>(sql, new { ProyectoId = proyectoId });
    }

    public async Task<int> CrearHitoAsync(CrearHitoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Proyectos.Hitos (ProyectoID, Nombre, FechaObjetivo, Orden)
            OUTPUT INSERTED.HitoID
            VALUES (@ProyectoID, @Nombre, @FechaObjetivo, @Orden)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task ActualizarHitoAsync(int hitoId, ActualizarHitoRequest r)
    {
        using var connection = _db.CreateConnection();

        var completado = r.Estado == "COMPLETADO";

        const string sql = @"
            UPDATE Proyectos.Hitos
            SET Nombre = @Nombre, FechaObjetivo = @FechaObjetivo, Estado = @Estado, Orden = @Orden,
                FechaCompletado = CASE WHEN @Completado = 1 THEN ISNULL(FechaCompletado, CAST(GETDATE() AS DATE)) ELSE NULL END
            WHERE HitoID = @HitoId";

        var filas = await connection.ExecuteAsync(sql, new
        {
            HitoId = hitoId, r.Nombre, r.FechaObjetivo, r.Estado, r.Orden, Completado = completado
        });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el hito {hitoId}.");
    }

    // ---------- Documentos ----------

    public async Task<IEnumerable<ProyectoDocumentoItem>> ListarDocumentosAsync(int proyectoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT DocumentoID, ProyectoID, TipoDocumento, NombreArchivo, ContentType, FechaSubida
            FROM Proyectos.ProyectoDocumentos
            WHERE ProyectoID = @ProyectoId
            ORDER BY FechaSubida DESC";

        return await connection.QueryAsync<ProyectoDocumentoItem>(sql, new { ProyectoId = proyectoId });
    }

    public async Task<int> SubirDocumentoAsync(int proyectoId, SubirDocumentoProyectoRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();
        var datos = Convert.FromBase64String(r.Base64);

        const string sql = @"
            INSERT INTO Proyectos.ProyectoDocumentos (ProyectoID, TipoDocumento, NombreArchivo, ContentType, Archivo, UsuarioID)
            OUTPUT INSERTED.DocumentoID
            VALUES (@ProyectoID, @TipoDocumento, @NombreArchivo, @ContentType, @Archivo, @UsuarioID)";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            ProyectoID = proyectoId, r.TipoDocumento, r.NombreArchivo, r.ContentType, Archivo = datos, UsuarioID = usuarioId
        });
    }

    private record DocumentoArchivo(byte[] Archivo, string ContentType, string NombreArchivo);

    public async Task<(byte[] Datos, string ContentType, string NombreArchivo)?> ObtenerDocumentoAsync(int documentoId)
    {
        using var connection = _db.CreateConnection();

        var resultado = await connection.QuerySingleOrDefaultAsync<DocumentoArchivo>(
            "SELECT Archivo, ContentType, NombreArchivo FROM Proyectos.ProyectoDocumentos WHERE DocumentoID = @DocumentoId",
            new { DocumentoId = documentoId });

        return resultado is null ? null : (resultado.Archivo, resultado.ContentType, resultado.NombreArchivo);
    }

    public async Task EliminarDocumentoAsync(int documentoId)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "DELETE FROM Proyectos.ProyectoDocumentos WHERE DocumentoID = @DocumentoId", new { DocumentoId = documentoId });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el documento {documentoId}.");
    }

    // ---------- Comentarios / Bitacora ----------

    public async Task<IEnumerable<ComentarioProyectoItem>> ListarComentariosAsync(int proyectoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT c.ComentarioID, c.ProyectoID, c.Texto, c.Fecha, u.Nombres + ' ' + u.Apellidos AS Usuario
            FROM Proyectos.Comentarios c
            LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = c.UsuarioID
            WHERE c.ProyectoID = @ProyectoId
            ORDER BY c.Fecha DESC";

        return await connection.QueryAsync<ComentarioProyectoItem>(sql, new { ProyectoId = proyectoId });
    }

    public async Task<int> CrearComentarioAsync(CrearComentarioRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Proyectos.Comentarios (ProyectoID, Texto, UsuarioID)
            OUTPUT INSERTED.ComentarioID
            VALUES (@ProyectoID, @Texto, @UsuarioID)";

        return await connection.ExecuteScalarAsync<int>(sql, new { r.ProyectoID, r.Texto, UsuarioID = usuarioId });
    }

    // ---------- Alertas ----------
    // Solo detecta -- no envia nada. Mismo criterio de "alerta interna" que
    // Clientes Frios (CRM) y Desviaciones (Planificacion).
    public async Task<IEnumerable<ProyectoAlertaItem>> ListarAlertasAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT p.ProyectoID, p.Nombre,
                   CAST(CASE WHEN p.FechaFin IS NOT NULL AND p.FechaFin < CAST(GETDATE() AS DATE)
                             AND p.Estado NOT IN ('FINALIZADO', 'CANCELADO') THEN 1 ELSE 0 END AS BIT) AS Atrasado,
                   CAST(CASE WHEN p.Presupuesto > 0 AND costo.Total > p.Presupuesto THEN 1 ELSE 0 END AS BIT) AS ConSobrecosto,
                   p.FechaFin, p.Presupuesto, costo.Total AS CostoTotal
            FROM Proyectos.Proyectos p
            CROSS APPLY (SELECT ISNULL(SUM(co.Valor), 0) AS Total FROM Proyectos.Costos co WHERE co.ProyectoID = p.ProyectoID) costo
            WHERE (p.FechaFin IS NOT NULL AND p.FechaFin < CAST(GETDATE() AS DATE) AND p.Estado NOT IN ('FINALIZADO', 'CANCELADO'))
               OR (p.Presupuesto > 0 AND costo.Total > p.Presupuesto)
            ORDER BY p.FechaFin";

        return await connection.QueryAsync<ProyectoAlertaItem>(sql);
    }
}
