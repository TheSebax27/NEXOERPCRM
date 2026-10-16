using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Rrhh.Dtos;

namespace NexoApi.Features.Rrhh;

public interface IRrhhService
{
    Task<int> CrearEmpleadoAsync(CrearEmpleadoRequest request);
    Task<IEnumerable<EmpleadoItem>> ListarEmpleadosAsync(bool soloActivos);
    Task ActualizarEmpleadoAsync(int empleadoId, ActualizarEmpleadoRequest request, int usuarioId);
    Task<(byte[] Datos, string ContentType)?> ObtenerFotoEmpleadoAsync(int empleadoId);
    Task ActualizarFotoEmpleadoAsync(int empleadoId, ActualizarFotoEmpleadoRequest request);
    Task EliminarFotoEmpleadoAsync(int empleadoId);

    Task<IEnumerable<DepartamentoItem>> ListarDepartamentosAsync();
    Task<int> CrearDepartamentoAsync(CrearDepartamentoRequest request);
    Task ActualizarDepartamentoAsync(int departamentoId, ActualizarDepartamentoRequest request);

    Task<IEnumerable<CargoItem>> ListarCargosAsync();
    Task<int> CrearCargoAsync(CrearCargoRequest request);
    Task ActualizarCargoAsync(int cargoId, ActualizarCargoRequest request);

    Task<IEnumerable<HistorialLaboralItem>> ListarHistorialLaboralAsync(int empleadoId);

    Task<IEnumerable<EmpleadoDocumentoItem>> ListarDocumentosAsync(int empleadoId);
    Task<int> SubirDocumentoAsync(int empleadoId, SubirDocumentoEmpleadoRequest request, int usuarioId);
    Task<(byte[] Datos, string ContentType, string NombreArchivo)?> ObtenerDocumentoAsync(int documentoId);
    Task EliminarDocumentoAsync(int documentoId);

    Task<IEnumerable<AusenciaItem>> ListarAusenciasAsync(int? empleadoId, string? estado);
    Task<int> CrearAusenciaAsync(CrearAusenciaRequest request, int usuarioId);
    Task ActualizarEstadoAusenciaAsync(int ausenciaId, ActualizarEstadoAusenciaRequest request);

    Task<IEnumerable<EvaluacionItem>> ListarEvaluacionesAsync(int empleadoId);
    Task<int> CrearEvaluacionAsync(CrearEvaluacionRequest request, int usuarioId);

    Task<IEnumerable<CapacitacionItem>> ListarCapacitacionesAsync(int empleadoId);
    Task<int> CrearCapacitacionAsync(CrearCapacitacionRequest request, int usuarioId);

    Task<IEnumerable<OrganigramaNodo>> ObtenerOrganigramaAsync();
}

public class RrhhService : IRrhhService
{
    private readonly IDbConnectionFactory _db;

    public RrhhService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<int> CrearEmpleadoAsync(CrearEmpleadoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Rrhh.Empleados (Nombres, Apellidos, CargoID, CentroCostoID, FechaIngreso, Telefono, Email, JefeDirectoID, TarifaHora, TipoSangre)
            OUTPUT INSERTED.EmpleadoID
            VALUES (@Nombres, @Apellidos, @CargoID, @CentroCostoID, @FechaIngreso, @Telefono, @Email, @JefeDirectoID, @TarifaHora, @TipoSangre)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task<IEnumerable<EmpleadoItem>> ListarEmpleadosAsync(bool soloActivos)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT e.EmpleadoID, e.Nombres, e.Apellidos,
                   e.CargoID, c.Nombre AS Cargo, c.DepartamentoID, dep.Nombre AS Departamento,
                   e.CentroCostoID, cc.Nombre AS CentroCosto, e.FechaIngreso, e.Telefono, e.Email, e.Estado,
                   CAST(CASE WHEN e.Foto IS NULL THEN 0 ELSE 1 END AS BIT) AS TieneFoto,
                   e.JefeDirectoID, jefe.Nombres + ' ' + jefe.Apellidos AS JefeDirecto,
                   CAST(CASE WHEN EXISTS (
                       SELECT 1 FROM Rrhh.Ausencias a
                       WHERE a.EmpleadoID = e.EmpleadoID AND a.Estado = 'APROBADA'
                         AND CAST(GETDATE() AS DATE) BETWEEN a.FechaInicio AND a.FechaFin
                   ) THEN 1 ELSE 0 END AS BIT) AS EnAusencia,
                   CAST(CASE WHEN EXISTS (
                       SELECT 1 FROM Seguridad.Usuarios u WHERE u.EmpleadoID = e.EmpleadoID
                   ) THEN 1 ELSE 0 END AS BIT) AS TieneUsuario,
                   e.TarifaHora, e.TipoSangre
            FROM Rrhh.Empleados e
            LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = e.CentroCostoID
            LEFT JOIN Rrhh.Cargos c ON c.CargoID = e.CargoID
            LEFT JOIN Rrhh.Departamentos dep ON dep.DepartamentoID = c.DepartamentoID
            LEFT JOIN Rrhh.Empleados jefe ON jefe.EmpleadoID = e.JefeDirectoID
            WHERE (@SoloActivos = 0 OR e.Estado = 1)
            ORDER BY e.Nombres, e.Apellidos";

        return await connection.QueryAsync<EmpleadoItem>(sql, new { SoloActivos = soloActivos });
    }

    private record EmpleadoActual(int? CargoID, int? CentroCostoID);
    private record NombreLookup(string Nombre);

    // Cambiar Cargo o Centro de Costo queda registrado automaticamente en el
    // Historial Laboral -- no es una entrada manual, se detecta comparando
    // el valor anterior contra el nuevo antes del UPDATE.
    public async Task ActualizarEmpleadoAsync(int empleadoId, ActualizarEmpleadoRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var actual = await connection.QuerySingleOrDefaultAsync<EmpleadoActual>(
                "SELECT CargoID, CentroCostoID FROM Rrhh.Empleados WHERE EmpleadoID = @EmpleadoId",
                new { EmpleadoId = empleadoId }, transaction);

            if (actual is null)
                throw new KeyNotFoundException($"No existe el empleado {empleadoId}.");

            if (r.JefeDirectoID == empleadoId)
                throw new InvalidOperationException("Un empleado no puede ser su propio jefe directo.");

            if (r.JefeDirectoID is not null)
            {
                // Evita ciclos: el nuevo jefe no puede ser, directa ni
                // indirectamente, subordinado del empleado que se esta editando.
                const string sqlCiclo = @"
                    WITH Subordinados AS (
                        SELECT EmpleadoID FROM Rrhh.Empleados WHERE JefeDirectoID = @EmpleadoId
                        UNION ALL
                        SELECT e.EmpleadoID FROM Rrhh.Empleados e JOIN Subordinados s ON e.JefeDirectoID = s.EmpleadoID
                    )
                    SELECT COUNT(*) FROM Subordinados WHERE EmpleadoID = @NuevoJefeId";

                var esCiclo = await connection.ExecuteScalarAsync<int>(sqlCiclo,
                    new { EmpleadoId = empleadoId, NuevoJefeId = r.JefeDirectoID }, transaction);

                if (esCiclo > 0)
                    throw new InvalidOperationException("Ese jefe directo generaría un ciclo en el organigrama (es subordinado de este empleado).");
            }

            const string sql = @"
                UPDATE Rrhh.Empleados
                SET Nombres = @Nombres, Apellidos = @Apellidos, CargoID = @CargoID, CentroCostoID = @CentroCostoID,
                    FechaIngreso = @FechaIngreso, Telefono = @Telefono, Email = @Email, Estado = @Estado,
                    JefeDirectoID = @JefeDirectoID, TarifaHora = @TarifaHora, TipoSangre = @TipoSangre
                WHERE EmpleadoID = @EmpleadoId";

            await connection.ExecuteAsync(sql, new
            {
                EmpleadoId = empleadoId,
                r.Nombres, r.Apellidos, r.CargoID, r.CentroCostoID, r.FechaIngreso, r.Telefono, r.Email, r.Estado, r.JefeDirectoID, r.TarifaHora, r.TipoSangre
            }, transaction);

            if (actual.CargoID != r.CargoID)
            {
                var nombreAnterior = actual.CargoID is null ? null : (await connection.QuerySingleOrDefaultAsync<NombreLookup>(
                    "SELECT Nombre FROM Rrhh.Cargos WHERE CargoID = @Id", new { Id = actual.CargoID }, transaction))?.Nombre;
                var nombreNuevo = r.CargoID is null ? null : (await connection.QuerySingleOrDefaultAsync<NombreLookup>(
                    "SELECT Nombre FROM Rrhh.Cargos WHERE CargoID = @Id", new { Id = r.CargoID }, transaction))?.Nombre;

                await connection.ExecuteAsync(
                    @"INSERT INTO Rrhh.HistorialLaboral (EmpleadoID, TipoEvento, ValorAnterior, ValorNuevo, UsuarioID)
                      VALUES (@EmpleadoID, 'CAMBIO_CARGO', @Anterior, @Nuevo, @UsuarioID)",
                    new { EmpleadoID = empleadoId, Anterior = nombreAnterior ?? "(sin cargo)", Nuevo = nombreNuevo ?? "(sin cargo)", UsuarioID = usuarioId },
                    transaction);
            }

            if (actual.CentroCostoID != r.CentroCostoID)
            {
                var nombreAnterior = actual.CentroCostoID is null ? null : (await connection.QuerySingleOrDefaultAsync<NombreLookup>(
                    "SELECT Nombre FROM Organizacion.CentrosCosto WHERE CentroCostoID = @Id", new { Id = actual.CentroCostoID }, transaction))?.Nombre;
                var nombreNuevo = r.CentroCostoID is null ? null : (await connection.QuerySingleOrDefaultAsync<NombreLookup>(
                    "SELECT Nombre FROM Organizacion.CentrosCosto WHERE CentroCostoID = @Id", new { Id = r.CentroCostoID }, transaction))?.Nombre;

                await connection.ExecuteAsync(
                    @"INSERT INTO Rrhh.HistorialLaboral (EmpleadoID, TipoEvento, ValorAnterior, ValorNuevo, UsuarioID)
                      VALUES (@EmpleadoID, 'CAMBIO_CENTRO_COSTO', @Anterior, @Nuevo, @UsuarioID)",
                    new { EmpleadoID = empleadoId, Anterior = nombreAnterior ?? "(sin centro de costo)", Nuevo = nombreNuevo ?? "(sin centro de costo)", UsuarioID = usuarioId },
                    transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private record FotoEmpleado(byte[] Foto, string FotoContentType);

    // Dapper no soporta mapear una fila directo a ValueTuple (ver leccion
    // 20.30 en CLAUDE.md) -- se usa un record privado, igual que se corrigio
    // en CatalogoService.ObtenerImagenArticuloAsync.
    public async Task<(byte[] Datos, string ContentType)?> ObtenerFotoEmpleadoAsync(int empleadoId)
    {
        using var connection = _db.CreateConnection();

        var resultado = await connection.QuerySingleOrDefaultAsync<FotoEmpleado>(
            "SELECT Foto, FotoContentType FROM Rrhh.Empleados WHERE EmpleadoID = @EmpleadoId AND Foto IS NOT NULL",
            new { EmpleadoId = empleadoId });

        return resultado is null ? null : (resultado.Foto, resultado.FotoContentType);
    }

    public async Task ActualizarFotoEmpleadoAsync(int empleadoId, ActualizarFotoEmpleadoRequest r)
    {
        using var connection = _db.CreateConnection();
        var datos = Convert.FromBase64String(r.Base64);

        var filas = await connection.ExecuteAsync(
            "UPDATE Rrhh.Empleados SET Foto = @Datos, FotoContentType = @ContentType WHERE EmpleadoID = @EmpleadoId",
            new { EmpleadoId = empleadoId, Datos = datos, r.ContentType });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el empleado {empleadoId}.");

        await connection.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET FotoPerfil = @Datos, FotoPerfilContentType = @ContentType WHERE EmpleadoID = @EmpleadoId",
            new { EmpleadoId = empleadoId, Datos = datos, r.ContentType });
    }

    public async Task EliminarFotoEmpleadoAsync(int empleadoId)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Rrhh.Empleados SET Foto = NULL, FotoContentType = NULL WHERE EmpleadoID = @EmpleadoId",
            new { EmpleadoId = empleadoId });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el empleado {empleadoId}.");

        await connection.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET FotoPerfil = NULL, FotoPerfilContentType = NULL WHERE EmpleadoID = @EmpleadoId",
            new { EmpleadoId = empleadoId });
    }

    // ---------- Departamentos y Cargos ----------

    public async Task<IEnumerable<DepartamentoItem>> ListarDepartamentosAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT d.DepartamentoID, d.Nombre, d.Estado,
                   (SELECT COUNT(*) FROM Rrhh.Cargos c WHERE c.DepartamentoID = d.DepartamentoID AND c.Estado = 1) AS TotalCargos
            FROM Rrhh.Departamentos d
            ORDER BY d.Nombre";

        return await connection.QueryAsync<DepartamentoItem>(sql);
    }

    public async Task<int> CrearDepartamentoAsync(CrearDepartamentoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Rrhh.Departamentos (Nombre)
            OUTPUT INSERTED.DepartamentoID
            VALUES (@Nombre)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task ActualizarDepartamentoAsync(int departamentoId, ActualizarDepartamentoRequest r)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Rrhh.Departamentos SET Nombre = @Nombre, Estado = @Estado WHERE DepartamentoID = @DepartamentoId",
            new { DepartamentoId = departamentoId, r.Nombre, r.Estado });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el departamento {departamentoId}.");
    }

    public async Task<IEnumerable<CargoItem>> ListarCargosAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT c.CargoID, c.Nombre, c.DepartamentoID, d.Nombre AS Departamento, c.Estado,
                   c.RolPredeterminadoID, r.Nombre AS RolPredeterminado
            FROM Rrhh.Cargos c
            LEFT JOIN Rrhh.Departamentos d ON d.DepartamentoID = c.DepartamentoID
            LEFT JOIN Seguridad.Roles r ON r.RolID = c.RolPredeterminadoID
            ORDER BY c.Nombre";

        return await connection.QueryAsync<CargoItem>(sql);
    }

    public async Task<int> CrearCargoAsync(CrearCargoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Rrhh.Cargos (Nombre, DepartamentoID, RolPredeterminadoID)
            OUTPUT INSERTED.CargoID
            VALUES (@Nombre, @DepartamentoID, @RolPredeterminadoID)";

        return await connection.ExecuteScalarAsync<int>(sql, new { r.Nombre, r.DepartamentoID, r.RolPredeterminadoID });
    }

    public async Task ActualizarCargoAsync(int cargoId, ActualizarCargoRequest r)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Rrhh.Cargos SET Nombre=@Nombre, DepartamentoID=@DepartamentoID, Estado=@Estado, RolPredeterminadoID=@RolPredeterminadoID WHERE CargoID=@CargoId",
            new { CargoId = cargoId, r.Nombre, r.DepartamentoID, r.Estado, r.RolPredeterminadoID });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el cargo {cargoId}.");
    }

    // ---------- Historial laboral ----------

    public async Task<IEnumerable<HistorialLaboralItem>> ListarHistorialLaboralAsync(int empleadoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT h.HistorialID, h.EmpleadoID, h.TipoEvento, h.ValorAnterior, h.ValorNuevo, h.Fecha, h.Notas,
                   u.Nombres + ' ' + u.Apellidos AS Usuario
            FROM Rrhh.HistorialLaboral h
            LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = h.UsuarioID
            WHERE h.EmpleadoID = @EmpleadoId
            ORDER BY h.Fecha DESC";

        return await connection.QueryAsync<HistorialLaboralItem>(sql, new { EmpleadoId = empleadoId });
    }

    // ---------- Documentos de empleado ----------

    public async Task<IEnumerable<EmpleadoDocumentoItem>> ListarDocumentosAsync(int empleadoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT DocumentoID, EmpleadoID, TipoDocumento, NombreArchivo, ContentType, FechaSubida
            FROM Rrhh.EmpleadoDocumentos
            WHERE EmpleadoID = @EmpleadoId
            ORDER BY FechaSubida DESC";

        return await connection.QueryAsync<EmpleadoDocumentoItem>(sql, new { EmpleadoId = empleadoId });
    }

    public async Task<int> SubirDocumentoAsync(int empleadoId, SubirDocumentoEmpleadoRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();
        var datos = Convert.FromBase64String(r.Base64);

        const string sql = @"
            INSERT INTO Rrhh.EmpleadoDocumentos (EmpleadoID, TipoDocumento, NombreArchivo, ContentType, Archivo, UsuarioID)
            OUTPUT INSERTED.DocumentoID
            VALUES (@EmpleadoID, @TipoDocumento, @NombreArchivo, @ContentType, @Archivo, @UsuarioID)";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            EmpleadoID = empleadoId, r.TipoDocumento, r.NombreArchivo, r.ContentType, Archivo = datos, UsuarioID = usuarioId
        });
    }

    private record DocumentoArchivo(byte[] Archivo, string ContentType, string NombreArchivo);

    public async Task<(byte[] Datos, string ContentType, string NombreArchivo)?> ObtenerDocumentoAsync(int documentoId)
    {
        using var connection = _db.CreateConnection();

        var resultado = await connection.QuerySingleOrDefaultAsync<DocumentoArchivo>(
            "SELECT Archivo, ContentType, NombreArchivo FROM Rrhh.EmpleadoDocumentos WHERE DocumentoID = @DocumentoId",
            new { DocumentoId = documentoId });

        return resultado is null ? null : (resultado.Archivo, resultado.ContentType, resultado.NombreArchivo);
    }

    public async Task EliminarDocumentoAsync(int documentoId)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "DELETE FROM Rrhh.EmpleadoDocumentos WHERE DocumentoID = @DocumentoId", new { DocumentoId = documentoId });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el documento {documentoId}.");
    }

    // ---------- Ausencias y Vacaciones ----------

    public async Task<IEnumerable<AusenciaItem>> ListarAusenciasAsync(int? empleadoId, string? estado)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT a.AusenciaID, a.EmpleadoID, e.Nombres + ' ' + e.Apellidos AS Empleado,
                   a.Tipo, a.FechaInicio, a.FechaFin, a.Motivo, a.Estado, a.FechaCreacion
            FROM Rrhh.Ausencias a
            JOIN Rrhh.Empleados e ON e.EmpleadoID = a.EmpleadoID
            WHERE (@EmpleadoId IS NULL OR a.EmpleadoID = @EmpleadoId)
              AND (@Estado IS NULL OR a.Estado = @Estado)
            ORDER BY a.FechaInicio DESC";

        return await connection.QueryAsync<AusenciaItem>(sql, new { EmpleadoId = empleadoId, Estado = estado });
    }

    public async Task<int> CrearAusenciaAsync(CrearAusenciaRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        if (r.FechaFin < r.FechaInicio)
            throw new InvalidOperationException("La fecha de fin no puede ser anterior a la fecha de inicio.");

        const string sql = @"
            INSERT INTO Rrhh.Ausencias (EmpleadoID, Tipo, FechaInicio, FechaFin, Motivo, UsuarioID)
            OUTPUT INSERTED.AusenciaID
            VALUES (@EmpleadoID, @Tipo, @FechaInicio, @FechaFin, @Motivo, @UsuarioID)";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            r.EmpleadoID, r.Tipo, r.FechaInicio, r.FechaFin, r.Motivo, UsuarioID = usuarioId
        });
    }

    public async Task ActualizarEstadoAusenciaAsync(int ausenciaId, ActualizarEstadoAusenciaRequest r)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Rrhh.Ausencias SET Estado = @Estado WHERE AusenciaID = @AusenciaId",
            new { AusenciaId = ausenciaId, r.Estado });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe la ausencia {ausenciaId}.");
    }

    // ---------- Evaluaciones de desempeño ----------

    public async Task<IEnumerable<EvaluacionItem>> ListarEvaluacionesAsync(int empleadoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT ev.EvaluacionID, ev.EmpleadoID, ev.ResponsableID,
                   r.Nombres + ' ' + r.Apellidos AS Responsable, ev.Fecha, ev.Calificacion, ev.Comentarios
            FROM Rrhh.Evaluaciones ev
            LEFT JOIN Rrhh.Empleados r ON r.EmpleadoID = ev.ResponsableID
            WHERE ev.EmpleadoID = @EmpleadoId
            ORDER BY ev.Fecha DESC";

        return await connection.QueryAsync<EvaluacionItem>(sql, new { EmpleadoId = empleadoId });
    }

    public async Task<int> CrearEvaluacionAsync(CrearEvaluacionRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        if (r.Calificacion < 1 || r.Calificacion > 5)
            throw new InvalidOperationException("La calificación debe estar entre 1.0 y 5.0.");

        const string sql = @"
            INSERT INTO Rrhh.Evaluaciones (EmpleadoID, ResponsableID, Fecha, Calificacion, Comentarios, UsuarioID)
            OUTPUT INSERTED.EvaluacionID
            VALUES (@EmpleadoID, @ResponsableID, @Fecha, @Calificacion, @Comentarios, @UsuarioID)";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            r.EmpleadoID, r.ResponsableID, r.Fecha, r.Calificacion, r.Comentarios, UsuarioID = usuarioId
        });
    }

    // ---------- Capacitaciones ----------

    public async Task<IEnumerable<CapacitacionItem>> ListarCapacitacionesAsync(int empleadoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT CapacitacionID, EmpleadoID, Nombre, Institucion, FechaRealizacion, FechaVencimiento
            FROM Rrhh.Capacitaciones
            WHERE EmpleadoID = @EmpleadoId
            ORDER BY FechaRealizacion DESC";

        return await connection.QueryAsync<CapacitacionItem>(sql, new { EmpleadoId = empleadoId });
    }

    public async Task<int> CrearCapacitacionAsync(CrearCapacitacionRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Rrhh.Capacitaciones (EmpleadoID, Nombre, Institucion, FechaRealizacion, FechaVencimiento, UsuarioID)
            OUTPUT INSERTED.CapacitacionID
            VALUES (@EmpleadoID, @Nombre, @Institucion, @FechaRealizacion, @FechaVencimiento, @UsuarioID)";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            r.EmpleadoID, r.Nombre, r.Institucion, r.FechaRealizacion, r.FechaVencimiento, UsuarioID = usuarioId
        });
    }

    // ---------- Organigrama ----------

    public async Task<IEnumerable<OrganigramaNodo>> ObtenerOrganigramaAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT e.EmpleadoID, e.Nombres, e.Apellidos, c.Nombre AS Cargo, e.JefeDirectoID
            FROM Rrhh.Empleados e
            LEFT JOIN Rrhh.Cargos c ON c.CargoID = e.CargoID
            WHERE e.Estado = 1
            ORDER BY e.Nombres, e.Apellidos";

        return await connection.QueryAsync<OrganigramaNodo>(sql);
    }
}
