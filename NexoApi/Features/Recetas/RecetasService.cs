using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Recetas.Dtos;

namespace NexoApi.Features.Recetas;

public interface IRecetasService
{
    Task<int> CrearAsync(CrearRecetaRequest request);
    Task<int> CrearNuevaVersionAsync(int recetaBaseId, CrearNuevaVersionRequest request);
    Task<IEnumerable<RecetaResumen>> ListarAsync(int? productoTerminadoId, bool soloActivas);
    Task<IEnumerable<RecetaDetalleItem>> ObtenerDetalleAsync(int recetaId);
    Task DesactivarAsync(int recetaId);
}

public class RecetasService : IRecetasService
{
    private readonly IDbConnectionFactory _db;

    public RecetasService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<int> CrearAsync(CrearRecetaRequest r)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        await ValidarInsumosPTAsync(connection, null, r.ProductoTerminadoID, r.Detalle);

        using var transaction = connection.BeginTransaction();

        try
        {
            const string sqlHeader = @"
                INSERT INTO Produccion.RecetaBOM
                    (ProductoTerminadoID, NombreReceta, Version, CantidadRendimientoBase)
                OUTPUT INSERTED.RecetaID
                VALUES (@ProductoTerminadoID, @NombreReceta, 1, @CantidadRendimientoBase)";

            var recetaId = await connection.ExecuteScalarAsync<int>(sqlHeader, new
            {
                r.ProductoTerminadoID,
                r.NombreReceta,
                r.CantidadRendimientoBase
            }, transaction);

            await InsertarDetalleAsync(connection, transaction, recetaId, r.Detalle);
            await InsertarMaquinariaAsync(connection, transaction, recetaId, r.Maquinas);

            transaction.Commit();
            return recetaId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private record RecetaBaseInfo(int ProductoTerminadoID, int VersionActual);

    public async Task<int> CrearNuevaVersionAsync(int recetaBaseId, CrearNuevaVersionRequest r)
    {
        using var connection = _db.CreateConnection();
        connection.Open();

        var ptId = await connection.ExecuteScalarAsync<int?>(
            "SELECT ProductoTerminadoID FROM Produccion.RecetaBOM WHERE RecetaID = @RecetaBaseId",
            new { RecetaBaseId = recetaBaseId });

        if (ptId.HasValue)
            await ValidarInsumosPTAsync(connection, recetaBaseId, ptId.Value, r.Detalle);

        using var transaction = connection.BeginTransaction();

        try
        {
            // Dapper no soporta mapear una fila directo a ValueTuple -- con
            // (int,int) esto fallaba con 500 en TODA llamada a este endpoint.
            var baseInfo = await connection.QuerySingleOrDefaultAsync<RecetaBaseInfo>(
                "SELECT ProductoTerminadoID, Version AS VersionActual FROM Produccion.RecetaBOM WHERE RecetaID = @RecetaBaseId",
                new { RecetaBaseId = recetaBaseId }, transaction);

            if (baseInfo is null)
                throw new KeyNotFoundException($"No existe la receta {recetaBaseId} para versionar.");

            const string sqlHeader = @"
                INSERT INTO Produccion.RecetaBOM
                    (ProductoTerminadoID, NombreReceta, Version, CantidadRendimientoBase)
                OUTPUT INSERTED.RecetaID
                VALUES (@ProductoTerminadoID, @NombreReceta, @NuevaVersion, @CantidadRendimientoBase)";

            var nuevaRecetaId = await connection.ExecuteScalarAsync<int>(sqlHeader, new
            {
                baseInfo.ProductoTerminadoID,
                r.NombreReceta,
                NuevaVersion = baseInfo.VersionActual + 1,
                r.CantidadRendimientoBase
            }, transaction);

            await InsertarDetalleAsync(connection, transaction, nuevaRecetaId, r.Detalle);
            await InsertarMaquinariaAsync(connection, transaction, nuevaRecetaId, r.Maquinas);

            await connection.ExecuteAsync(
                "UPDATE Produccion.RecetaBOM SET Estado = 0 WHERE RecetaID = @RecetaBaseId",
                new { RecetaBaseId = recetaBaseId }, transaction);

            transaction.Commit();
            return nuevaRecetaId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task InsertarDetalleAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction transaction,
        int recetaId, List<DetalleRecetaRequest> detalle)
    {
        const string sqlDetalle = @"
            INSERT INTO Produccion.RecetaBOM_Detalle
                (RecetaID, InsumoID, CantidadRequerida, PorcentajeMermaEstandar, CentroTrabajoID, Orden)
            VALUES
                (@RecetaID, @InsumoID, @CantidadRequerida, @PorcentajeMermaEstandar, @CentroTrabajoID, @Orden)";

        if (detalle.Any(d => d.CantidadRequerida <= 0))
            throw new InvalidOperationException("Todos los insumos deben tener CantidadRequerida mayor a 0.");

        foreach (var linea in detalle)
        {
            await connection.ExecuteAsync(sqlDetalle, new
            {
                RecetaID = recetaId,
                linea.InsumoID,
                linea.CantidadRequerida,
                linea.PorcentajeMermaEstandar,
                linea.CentroTrabajoID,
                linea.Orden
            }, transaction);
        }
    }

    private static async Task InsertarMaquinariaAsync(
        System.Data.IDbConnection conn, System.Data.IDbTransaction tx,
        int recetaId, List<MaquinariaRecetaInput>? maquinas)
    {
        if (maquinas is null || maquinas.Count == 0) return;
        const string sql = """
            INSERT INTO Produccion.RecetaMaquinaria (RecetaID, MaquinariaID, HorasEstimadasPorLote, Notas)
            VALUES (@RecetaID, @MaquinariaID, @HorasEstimadasPorLote, @Notas)
            """;
        foreach (var m in maquinas)
            await conn.ExecuteAsync(sql, new { RecetaID = recetaId, m.MaquinariaID, m.HorasEstimadasPorLote, m.Notas }, tx);
    }

    public async Task<IEnumerable<RecetaResumen>> ListarAsync(int? productoTerminadoId, bool soloActivas)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT r.RecetaID, r.ProductoTerminadoID, a.Nombre AS ProductoTerminado, r.NombreReceta, r.Version,
                   r.CantidadRendimientoBase, ISNULL(p.Presentacion, ISNULL(a.PresentacionCodigo, '')) AS UnidadRendimiento, r.Estado
            FROM Produccion.RecetaBOM r
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = r.ProductoTerminadoID
            LEFT JOIN Catalogo.Presentacion p ON p.Codigo = a.PresentacionCodigo
            WHERE (@ProductoTerminadoId IS NULL OR r.ProductoTerminadoID = @ProductoTerminadoId)
              AND (@SoloActivas = 0 OR r.Estado = 1)
            ORDER BY a.Nombre, r.Version DESC";

        return await connection.QueryAsync<RecetaResumen>(sql, new
        {
            ProductoTerminadoId = productoTerminadoId,
            SoloActivas = soloActivas
        });
    }

    public async Task<IEnumerable<RecetaDetalleItem>> ObtenerDetalleAsync(int recetaId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT d.RecetaDetalleID, d.InsumoID, a.Nombre AS Insumo, d.CantidadRequerida,
                   ISNULL(NULLIF(ISNULL(p.Presentacion, a.PresentacionCodigo), ''), 'Sin unidad') AS Unidad,
                   d.PorcentajeMermaEstandar, d.CentroTrabajoID, d.Orden
            FROM Produccion.RecetaBOM_Detalle d
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = d.InsumoID
            LEFT JOIN Catalogo.Presentacion p ON p.Codigo = a.PresentacionCodigo
            WHERE d.RecetaID = @RecetaId
            ORDER BY d.Orden";

        return await connection.QueryAsync<RecetaDetalleItem>(sql, new { RecetaId = recetaId });
    }

    // Valida que ningun insumo PT cause auto-referencia ni ciclo directo (A→B, B→A).
    // Se llama antes de abrir la transaccion para que el error sea limpio.
    private static async Task ValidarInsumosPTAsync(
        System.Data.IDbConnection connection,
        int? recetaBaseId,
        int productoTerminadoID,
        List<DetalleRecetaRequest> detalle)
    {
        var ptInsumos = detalle.Select(d => d.InsumoID).Where(id => id == productoTerminadoID).ToList();
        if (ptInsumos.Count > 0)
            throw new InvalidOperationException(
                "Un producto terminado no puede ser insumo de su propia receta (auto-referencia).");

        // Ciclo directo: si alguno de los PT-insumos tiene una receta activa
        // que usa el PT actual como insumo.
        var insumoIds = detalle.Select(d => d.InsumoID).Distinct().ToList();
        if (insumoIds.Count == 0) return;

        const string sqlCiclo = @"
            SELECT TOP 1 a.Nombre
            FROM Produccion.RecetaBOM r
            JOIN Produccion.RecetaBOM_Detalle bd ON bd.RecetaID = r.RecetaID
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = r.ProductoTerminadoID
            WHERE r.Estado = 1
              AND (@RecetaBaseId IS NULL OR r.RecetaID <> @RecetaBaseId)
              AND r.ProductoTerminadoID IN @InsumoIds
              AND bd.InsumoID = @ProductoTerminadoID";

        var nombreCiclo = await connection.ExecuteScalarAsync<string?>(sqlCiclo, new
        {
            InsumoIds = insumoIds,
            ProductoTerminadoID = productoTerminadoID,
            RecetaBaseId = recetaBaseId
        });

        if (nombreCiclo is not null)
            throw new InvalidOperationException(
                $"Referencia circular: «{nombreCiclo}» ya usa este producto terminado como insumo.");
    }

    // No se borra fisicamente: las ordenes de produccion ya ejecutadas quedan
    // enlazadas a esta receta por RecetaID, asi que se desactiva (igual que
    // Bodegas, Articulos, Clientes, Proveedores en este mismo sistema) para no
    // romper ese historial.
    public async Task DesactivarAsync(int recetaId)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Produccion.RecetaBOM SET Estado = 0 WHERE RecetaID = @RecetaId",
            new { RecetaId = recetaId });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe la receta {recetaId}.");
    }
}