using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Produccion.Dtos;

namespace NexoApi.Features.Produccion;

public interface IMaquinariaService
{
    Task<IEnumerable<TipoMaquinariaItem>> ListarTiposAsync();
    Task<int> CrearTipoAsync(CrearTipoMaquinariaRequest r);

    Task<IEnumerable<MaquinariaItem>> ListarAsync(int? tipoId, string? estado, int? centroTrabajoId);
    Task<MaquinariaDetalle?> ObtenerAsync(int id);
    Task<int> CrearAsync(CrearMaquinariaRequest r);
    Task ActualizarAsync(int id, ActualizarMaquinariaRequest r);

    Task<IEnumerable<MantenimientoItem>> ListarMantenimientosAsync(int maquinariaId);
    Task RegistrarMantenimientoAsync(int maquinariaId, CrearMantenimientoRequest r, int usuarioId);

    Task<IEnumerable<MaquinariaEnMantenimientoItem>> ListarEnMantenimientoAsync();

    Task<IEnumerable<RecetaMaquinariaItem>> ListarMaquinasRecetaAsync(int recetaId);
    Task<IEnumerable<OrdenMaquinariaItem>> ListarMaquinasOrdenAsync(int ordenId);
    Task GuardarMaquinasOrdenAsync(int ordenId, List<MaquinariaOrdenInput> maquinas);
    Task<MaquinariaEstadisticas> GetEstadisticasAsync(int maquinariaId);

    Task<(byte[] Data, string ContentType)?> ObtenerFotoAsync(int id);
    Task ActualizarFotoAsync(int id, string base64, string contentType);
    Task EliminarFotoAsync(int id);
}

public class MaquinariaService(IDbConnectionFactory db) : IMaquinariaService
{
    // ── Tipos ────────────────────────────────────────────────────

    public async Task<IEnumerable<TipoMaquinariaItem>> ListarTiposAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<TipoMaquinariaItem>(
            "SELECT TipoMaquinariaID, Nombre, Descripcion, Activo FROM Produccion.TiposMaquinaria WHERE Activo=1 ORDER BY Nombre");
    }

    public async Task<int> CrearTipoAsync(CrearTipoMaquinariaRequest r)
    {
        using var conn = db.CreateConnection();
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO Produccion.TiposMaquinaria (Nombre, Descripcion)
            OUTPUT INSERTED.TipoMaquinariaID
            VALUES (@Nombre, @Descripcion)
            """, r);
    }

    // ── Maquinaria ───────────────────────────────────────────────

    private record MaquinariaCruda(
        int MaquinariaID, string Codigo, string Nombre,
        int TipoMaquinariaID, string TipoMaquinaria,
        int? CentroTrabajoID, string? CentroTrabajo,
        string Estado, string? Marca, string? Modelo,
        decimal? CostoHoraOperacion, DateTime? ProximoMantenimiento,
        bool TieneFoto);

    public async Task<IEnumerable<MaquinariaItem>> ListarAsync(int? tipoId, string? estado, int? centroTrabajoId)
    {
        using var conn = db.CreateConnection();
        var crudas = await conn.QueryAsync<MaquinariaCruda>("""
            SELECT m.MaquinariaID, m.Codigo, m.Nombre,
                   m.TipoMaquinariaID, t.Nombre AS TipoMaquinaria,
                   m.CentroTrabajoID, ct.Nombre AS CentroTrabajo,
                   m.Estado, m.Marca, m.Modelo, m.CostoHoraOperacion,
                   (SELECT TOP 1 mm.ProximoMantenimiento
                    FROM Produccion.MantenimientoMaquinaria mm
                    WHERE mm.MaquinariaID=m.MaquinariaID AND mm.ProximoMantenimiento IS NOT NULL
                    ORDER BY mm.FechaRealizado DESC) AS ProximoMantenimiento,
                   CASE WHEN m.Foto IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS TieneFoto
            FROM Produccion.Maquinaria m
            JOIN Produccion.TiposMaquinaria t ON t.TipoMaquinariaID=m.TipoMaquinariaID
            LEFT JOIN Organizacion.CentrosTrabajo ct ON ct.CentroTrabajoID=m.CentroTrabajoID
            WHERE (@tipoId IS NULL OR m.TipoMaquinariaID=@tipoId)
              AND (@estado IS NULL OR m.Estado=@estado)
              AND (@centroTrabajoId IS NULL OR m.CentroTrabajoID=@centroTrabajoId)
            ORDER BY m.Estado, m.Nombre
            """, new { tipoId, estado, centroTrabajoId });

        var hoy = NexoApi.Common.ColombiaClock.Today;
        return crudas.Select(c =>
        {
            var proxFecha = c.ProximoMantenimiento.HasValue
                ? DateOnly.FromDateTime(c.ProximoMantenimiento.Value) : (DateOnly?)null;
            var vencido = proxFecha.HasValue && proxFecha.Value < DateOnly.FromDateTime(hoy);
            return new MaquinariaItem(
                c.MaquinariaID, c.Codigo, c.Nombre,
                c.TipoMaquinariaID, c.TipoMaquinaria,
                c.CentroTrabajoID, c.CentroTrabajo,
                c.Estado, c.Marca, c.Modelo, c.CostoHoraOperacion,
                proxFecha, vencido, c.TieneFoto);
        });
    }

    private record DetalleCrudo(
        int MaquinariaID, string Codigo, string Nombre,
        int TipoMaquinariaID, string TipoMaquinaria,
        int? CentroTrabajoID, string? CentroTrabajo, string Estado,
        string? Marca, string? Modelo, string? NumeroSerie,
        DateTime? FechaAdquisicion, int? VidaUtilAnios,
        decimal? CostoAdquisicion, decimal? CostoHoraOperacion,
        decimal? CapacidadMaxima, string? UnidadCapacidad,
        string? UbicacionFisica, string? Notas, DateTime FechaCreacion,
        bool TieneFoto);

    public async Task<MaquinariaDetalle?> ObtenerAsync(int id)
    {
        using var conn = db.CreateConnection();
        var c = await conn.QueryFirstOrDefaultAsync<DetalleCrudo>("""
            SELECT m.MaquinariaID, m.Codigo, m.Nombre,
                   m.TipoMaquinariaID, t.Nombre AS TipoMaquinaria,
                   m.CentroTrabajoID, ct.Nombre AS CentroTrabajo,
                   m.Estado, m.Marca, m.Modelo, m.NumeroSerie,
                   m.FechaAdquisicion, m.VidaUtilAnios,
                   m.CostoAdquisicion, m.CostoHoraOperacion,
                   m.CapacidadMaxima, m.UnidadCapacidad,
                   m.UbicacionFisica, m.Notas, m.FechaCreacion,
                   CASE WHEN m.Foto IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS TieneFoto
            FROM Produccion.Maquinaria m
            JOIN Produccion.TiposMaquinaria t ON t.TipoMaquinariaID=m.TipoMaquinariaID
            LEFT JOIN Organizacion.CentrosTrabajo ct ON ct.CentroTrabajoID=m.CentroTrabajoID
            WHERE m.MaquinariaID=@id
            """, new { id });

        if (c is null) return null;
        return new MaquinariaDetalle(
            c.MaquinariaID, c.Codigo, c.Nombre,
            c.TipoMaquinariaID, c.TipoMaquinaria,
            c.CentroTrabajoID, c.CentroTrabajo, c.Estado,
            c.Marca, c.Modelo, c.NumeroSerie,
            c.FechaAdquisicion.HasValue ? DateOnly.FromDateTime(c.FechaAdquisicion.Value) : null,
            c.VidaUtilAnios, c.CostoAdquisicion, c.CostoHoraOperacion,
            c.CapacidadMaxima, c.UnidadCapacidad, c.UbicacionFisica, c.Notas, c.FechaCreacion,
            c.TieneFoto);
    }

    public async Task<int> CrearAsync(CrearMaquinariaRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Codigo)) throw new InvalidOperationException("El código es obligatorio.");
        if (string.IsNullOrWhiteSpace(r.Nombre))  throw new InvalidOperationException("El nombre es obligatorio.");

        using var conn = db.CreateConnection();
        try
        {
            return await conn.ExecuteScalarAsync<int>("""
                INSERT INTO Produccion.Maquinaria
                    (Codigo, Nombre, TipoMaquinariaID, CentroTrabajoID,
                     Marca, Modelo, NumeroSerie, FechaAdquisicion, VidaUtilAnios,
                     CostoAdquisicion, CostoHoraOperacion, CapacidadMaxima, UnidadCapacidad,
                     UbicacionFisica, Notas)
                OUTPUT INSERTED.MaquinariaID
                VALUES
                    (@Codigo, @Nombre, @TipoMaquinariaID, @CentroTrabajoID,
                     @Marca, @Modelo, @NumeroSerie, @FechaAdquisicion, @VidaUtilAnios,
                     @CostoAdquisicion, @CostoHoraOperacion, @CapacidadMaxima, @UnidadCapacidad,
                     @UbicacionFisica, @Notas)
                """, new
            {
                r.Codigo, r.Nombre, r.TipoMaquinariaID, r.CentroTrabajoID,
                r.Marca, r.Modelo, r.NumeroSerie,
                FechaAdquisicion = r.FechaAdquisicion.HasValue ? (DateTime?)r.FechaAdquisicion.Value.ToDateTime(TimeOnly.MinValue) : null,
                r.VidaUtilAnios, r.CostoAdquisicion, r.CostoHoraOperacion,
                r.CapacidadMaxima, r.UnidadCapacidad, r.UbicacionFisica, r.Notas
            });
        }
        catch (Exception ex) when (ex.Message.Contains("UQ_Maquinaria_Codigo"))
        {
            throw new InvalidOperationException($"Ya existe una máquina con el código «{r.Codigo}».");
        }
    }

    public async Task ActualizarAsync(int id, ActualizarMaquinariaRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Codigo)) throw new InvalidOperationException("El código es obligatorio.");
        if (string.IsNullOrWhiteSpace(r.Nombre))  throw new InvalidOperationException("El nombre es obligatorio.");

        using var conn = db.CreateConnection();
        var affected = await conn.ExecuteAsync("""
            UPDATE Produccion.Maquinaria SET
                Codigo=@Codigo, Nombre=@Nombre, TipoMaquinariaID=@TipoMaquinariaID,
                CentroTrabajoID=@CentroTrabajoID, Estado=@Estado,
                Marca=@Marca, Modelo=@Modelo, NumeroSerie=@NumeroSerie,
                FechaAdquisicion=@FechaAdquisicion, VidaUtilAnios=@VidaUtilAnios,
                CostoAdquisicion=@CostoAdquisicion, CostoHoraOperacion=@CostoHoraOperacion,
                CapacidadMaxima=@CapacidadMaxima, UnidadCapacidad=@UnidadCapacidad,
                UbicacionFisica=@UbicacionFisica, Notas=@Notas
            WHERE MaquinariaID=@id
            """, new
        {
            id, r.Codigo, r.Nombre, r.TipoMaquinariaID, r.CentroTrabajoID, r.Estado,
            r.Marca, r.Modelo, r.NumeroSerie,
            FechaAdquisicion = r.FechaAdquisicion.HasValue ? (DateTime?)r.FechaAdquisicion.Value.ToDateTime(TimeOnly.MinValue) : null,
            r.VidaUtilAnios, r.CostoAdquisicion, r.CostoHoraOperacion,
            r.CapacidadMaxima, r.UnidadCapacidad, r.UbicacionFisica, r.Notas
        });

        if (affected == 0) throw new KeyNotFoundException("Máquina no encontrada.");
    }

    // ── Mantenimientos ──────────────────────────────────────────

    private record MantCrudo(
        int MantenimientoID, int MaquinariaID, string Maquinaria,
        string TipoMantenimiento, DateTime FechaRealizado,
        string Descripcion, decimal? Costo,
        decimal? HorasFueraServicio, string? Tecnico,
        DateTime? ProximoMantenimiento, string? Observaciones,
        string? Usuario, DateTime FechaRegistro);

    public async Task<IEnumerable<MantenimientoItem>> ListarMantenimientosAsync(int maquinariaId)
    {
        using var conn = db.CreateConnection();
        var crudos = await conn.QueryAsync<MantCrudo>("""
            SELECT mm.MantenimientoID, mm.MaquinariaID, m.Nombre AS Maquinaria,
                   mm.TipoMantenimiento, mm.FechaRealizado, mm.Descripcion,
                   mm.Costo, mm.HorasFueraServicio, mm.Tecnico,
                   mm.ProximoMantenimiento, mm.Observaciones,
                   CONCAT(u.Nombres, ' ', u.Apellidos) AS Usuario, mm.FechaRegistro
            FROM Produccion.MantenimientoMaquinaria mm
            JOIN Produccion.Maquinaria m ON m.MaquinariaID=mm.MaquinariaID
            LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID=mm.UsuarioID
            WHERE mm.MaquinariaID=@maquinariaId
            ORDER BY mm.FechaRealizado DESC, mm.MantenimientoID DESC
            """, new { maquinariaId });

        return crudos.Select(c => new MantenimientoItem(
            c.MantenimientoID, c.MaquinariaID, c.Maquinaria,
            c.TipoMantenimiento, DateOnly.FromDateTime(c.FechaRealizado),
            c.Descripcion, c.Costo, c.HorasFueraServicio, c.Tecnico,
            c.ProximoMantenimiento.HasValue ? DateOnly.FromDateTime(c.ProximoMantenimiento.Value) : null,
            c.Observaciones, c.Usuario, c.FechaRegistro));
    }

    public async Task RegistrarMantenimientoAsync(int maquinariaId, CrearMantenimientoRequest r, int usuarioId)
    {
        if (string.IsNullOrWhiteSpace(r.Descripcion))
            throw new InvalidOperationException("La descripción del mantenimiento es obligatoria.");

        using var conn = db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync("""
            INSERT INTO Produccion.MantenimientoMaquinaria
                (MaquinariaID, TipoMantenimiento, FechaRealizado, Descripcion,
                 Costo, HorasFueraServicio, Tecnico, ProximoMantenimiento,
                 Observaciones, UsuarioID)
            VALUES
                (@maquinariaId, @TipoMantenimiento, @FechaRealizado, @Descripcion,
                 @Costo, @HorasFueraServicio, @Tecnico, @ProximoMantenimiento,
                 @Observaciones, @usuarioId)
            """, new
        {
            maquinariaId, r.TipoMantenimiento,
            FechaRealizado = r.FechaRealizado.ToDateTime(TimeOnly.MinValue),
            r.Descripcion, r.Costo, r.HorasFueraServicio, r.Tecnico,
            ProximoMantenimiento = r.ProximoMantenimiento.HasValue ? (DateTime?)r.ProximoMantenimiento.Value.ToDateTime(TimeOnly.MinValue) : null,
            r.Observaciones, usuarioId
        }, tx);

        if (!string.IsNullOrWhiteSpace(r.NuevoEstadoMaquinaria))
        {
            await conn.ExecuteAsync(
                "UPDATE Produccion.Maquinaria SET Estado=@estado WHERE MaquinariaID=@maquinariaId",
                new { estado = r.NuevoEstadoMaquinaria, maquinariaId }, tx);
        }

        tx.Commit();
    }

    public async Task<IEnumerable<MaquinariaEnMantenimientoItem>> ListarEnMantenimientoAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<MaquinariaEnMantenimientoItem>("""
            SELECT m.MaquinariaID, m.Codigo, m.Nombre,
                   m.CentroTrabajoID, ct.Nombre AS CentroTrabajo
            FROM Produccion.Maquinaria m
            LEFT JOIN Organizacion.CentrosTrabajo ct ON ct.CentroTrabajoID=m.CentroTrabajoID
            WHERE m.Estado='EnMantenimiento'
            ORDER BY ct.Nombre, m.Nombre
            """);
    }

    // ── Enlace con Recetas y Órdenes ────────────────────────────

    public async Task<IEnumerable<RecetaMaquinariaItem>> ListarMaquinasRecetaAsync(int recetaId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<RecetaMaquinariaItem>("""
            SELECT m.MaquinariaID, m.Codigo, m.Nombre, t.Nombre AS TipoMaquinaria,
                   rm.HorasEstimadasPorLote, rm.Notas
            FROM Produccion.RecetaMaquinaria rm
            JOIN Produccion.Maquinaria m ON m.MaquinariaID=rm.MaquinariaID
            JOIN Produccion.TiposMaquinaria t ON t.TipoMaquinariaID=m.TipoMaquinariaID
            WHERE rm.RecetaID=@recetaId
            ORDER BY m.Nombre
            """, new { recetaId });
    }

    public async Task<IEnumerable<OrdenMaquinariaItem>> ListarMaquinasOrdenAsync(int ordenId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<OrdenMaquinariaItem>("""
            SELECT m.MaquinariaID, m.Codigo, m.Nombre, t.Nombre AS TipoMaquinaria,
                   om.HorasReales, om.Notas
            FROM Produccion.OrdenMaquinaria om
            JOIN Produccion.Maquinaria m ON m.MaquinariaID=om.MaquinariaID
            JOIN Produccion.TiposMaquinaria t ON t.TipoMaquinariaID=m.TipoMaquinariaID
            WHERE om.OrdenProduccionID=@ordenId
            ORDER BY m.Nombre
            """, new { ordenId });
    }

    public async Task GuardarMaquinasOrdenAsync(int ordenId, List<MaquinariaOrdenInput> maquinas)
    {
        using var conn = db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(
            "DELETE FROM Produccion.OrdenMaquinaria WHERE OrdenProduccionID=@ordenId",
            new { ordenId }, tx);
        if (maquinas.Count > 0)
        {
            const string sql = """
                INSERT INTO Produccion.OrdenMaquinaria (OrdenProduccionID, MaquinariaID, HorasReales, Notas)
                VALUES (@OrdenProduccionID, @MaquinariaID, @HorasReales, @Notas)
                """;
            foreach (var m in maquinas)
                await conn.ExecuteAsync(sql, new { OrdenProduccionID = ordenId, m.MaquinariaID, m.HorasReales, m.Notas }, tx);
        }
        tx.Commit();
    }

    private record EstadisticasCruda(int TotalOrdenes, decimal TotalHorasOrdenes, int TotalRecetas);

    public async Task<MaquinariaEstadisticas> GetEstadisticasAsync(int maquinariaId)
    {
        using var conn = db.CreateConnection();
        var cruda = await conn.QueryFirstOrDefaultAsync<EstadisticasCruda>("""
            SELECT
                (SELECT COUNT(*) FROM Produccion.OrdenMaquinaria WHERE MaquinariaID=@id)            AS TotalOrdenes,
                (SELECT ISNULL(SUM(HorasReales),0) FROM Produccion.OrdenMaquinaria WHERE MaquinariaID=@id) AS TotalHorasOrdenes,
                (SELECT COUNT(*) FROM Produccion.RecetaMaquinaria WHERE MaquinariaID=@id)            AS TotalRecetas
            """, new { id = maquinariaId });
        if (cruda is null) return new(0, 0, null, 0);
        decimal? promedio = cruda.TotalOrdenes > 0 ? cruda.TotalHorasOrdenes / cruda.TotalOrdenes : null;
        return new(cruda.TotalOrdenes, cruda.TotalHorasOrdenes, promedio, cruda.TotalRecetas);
    }

    // ── Foto ────────────────────────────────────────────────────

    private record FotoCruda(byte[] Foto, string? FotoContentType);

    public async Task<(byte[] Data, string ContentType)?> ObtenerFotoAsync(int id)
    {
        using var conn = db.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync<FotoCruda>(
            "SELECT Foto, FotoContentType FROM Produccion.Maquinaria WHERE MaquinariaID=@id AND Foto IS NOT NULL",
            new { id });
        if (row is null) return null;
        return (row.Foto, row.FotoContentType ?? "image/jpeg");
    }

    public async Task ActualizarFotoAsync(int id, string base64, string contentType)
    {
        var data = Convert.FromBase64String(base64);
        using var conn = db.CreateConnection();
        var affected = await conn.ExecuteAsync(
            "UPDATE Produccion.Maquinaria SET Foto=@data, FotoContentType=@contentType WHERE MaquinariaID=@id",
            new { data, contentType, id });
        if (affected == 0) throw new KeyNotFoundException("Máquina no encontrada.");
    }

    public async Task EliminarFotoAsync(int id)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Produccion.Maquinaria SET Foto=NULL, FotoContentType=NULL WHERE MaquinariaID=@id",
            new { id });
    }
}
