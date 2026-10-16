using System.Data;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Email;
using NexoApi.Features.Produccion.Dtos;

namespace NexoApi.Features.Produccion;

public interface IOrdenesProduccionService
{
    Task<int> CrearAsync(CrearOrdenProduccionRequest request, int usuarioCreaId);
    Task<IEnumerable<OrdenProduccionResumen>> ListarAsync(int? centroCostoId, string? estado);
    Task<OrdenProduccionResumen?> ObtenerAsync(int ordenProduccionId);
    Task<OrdenProduccionDetalleEdicion?> ObtenerParaEdicionAsync(int ordenProduccionId);
    Task ActualizarAsync(int ordenProduccionId, ActualizarOrdenProduccionRequest request);
    Task CancelarAsync(int ordenProduccionId);
    Task LiberarAsync(int ordenProduccionId, int usuarioId);
    Task IniciarAsync(int ordenProduccionId, int usuarioId);
    Task<(decimal CostoUnitarioReal, int LoteProductoTerminadoID)> CerrarAsync(int ordenProduccionId, CerrarOrdenProduccionRequest request, int usuarioId);
    Task AjustarConsumoAsync(long consumoId, AjustarConsumoRealRequest request, int usuarioId);
    Task<IEnumerable<TipoProduccionItem>> ListarTiposProduccionAsync();
    Task<IEnumerable<ConsumoOpItem>> ListarConsumosAsync(int ordenProduccionId);
    Task<IEnumerable<MotivoExcesoItem>> ListarMotivosExcesoAsync();
    Task<IEnumerable<StockLineaItem>> VerificarStockOrdenAsync(int ordenProduccionId);
    Task<string> GenerarSiguienteCodigoOPAsync(string prefijo);
    Task<int> MarcarRetrasadasAsync();
    Task<string> GenerarSiguienteNumeroLoteAsync(string prefijo);
    Task<IEnumerable<FaltanteMaterialItem>> ListarFaltantesMaterialesAsync();
    Task<IEnumerable<DesviacionConsumoItem>> ListarDesviacionesConsumoAsync(DateTime? desde, DateTime? hasta);
    Task<IEnumerable<SubproductoRecetaItem>> ListarSubproductosRecetaAsync(int recetaId);
    Task<int> AgregarSubproductoRecetaAsync(int recetaId, AgregarSubproductoRecetaRequest request);
    Task EliminarSubproductoRecetaAsync(int subproductoBomId);
    Task<IEnumerable<SubproductoOpItem>> ListarSubproductosOpAsync(int ordenProduccionId);
}

public record StockLineaItem(string Articulo, string Unidad, decimal CantidadRequerida, decimal StockDisponible);

public class OrdenesProduccionService : IOrdenesProduccionService
{
    private record CerrarResultado(string Resultado, decimal CostoUnitarioReal, int LoteProductoTerminadoID);

    private readonly IDbConnectionFactory _db;
    private readonly IEmailService _email;

    public OrdenesProduccionService(IDbConnectionFactory db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<int> CrearAsync(CrearOrdenProduccionRequest r, int usuarioCreaId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Produccion.OrdenesProduccion
                (CodigoOP, TipoProduccionID, EstadoOPID, ProductoTerminadoID, RecetaID, CantidadProgramada,
                 ClienteID, CentroCostoDestinoID, BodegaOrigenMPID, BodegaDestinoPTID, CentroTrabajoID,
                 FechaPlanificada, UsuarioCreaID, Observaciones)
            OUTPUT INSERTED.OrdenProduccionID
            VALUES
                (@CodigoOP, @TipoProduccionID,
                 (SELECT EstadoOPID FROM Produccion.EstadosOP WHERE Nombre = 'Planificada'),
                 @ProductoTerminadoID, @RecetaID, @CantidadProgramada,
                 @ClienteID, @CentroCostoDestinoID, @BodegaOrigenMPID, @BodegaDestinoPTID, @CentroTrabajoID,
                 @FechaPlanificada, @UsuarioCreaID, @Observaciones)";

        connection.Open();
        var ordenId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            r.CodigoOP,
            r.TipoProduccionID,
            r.ProductoTerminadoID,
            r.RecetaID,
            r.CantidadProgramada,
            r.ClienteID,
            r.CentroCostoDestinoID,
            r.BodegaOrigenMPID,
            r.BodegaDestinoPTID,
            r.CentroTrabajoID,
            r.FechaPlanificada,
            UsuarioCreaID = usuarioCreaId,
            r.Observaciones
        });

        if (r.Maquinas is { Count: > 0 })
        {
            const string sqlMaq = """
                INSERT INTO Produccion.OrdenMaquinaria (OrdenProduccionID, MaquinariaID, HorasReales, Notas)
                VALUES (@OrdenProduccionID, @MaquinariaID, @HorasReales, @Notas)
                """;
            foreach (var m in r.Maquinas)
                await connection.ExecuteAsync(sqlMaq, new { OrdenProduccionID = ordenId, m.MaquinariaID, m.HorasReales, m.Notas });
        }

        if (r.Empleados is { Count: > 0 })
        {
            const string sqlEmp = """
                INSERT INTO Produccion.OrdenEmpleado (OrdenProduccionID, EmpleadoID, HorasReales, Notas)
                VALUES (@OrdenProduccionID, @EmpleadoID, @HorasReales, @Notas)
                """;
            foreach (var e in r.Empleados)
                await connection.ExecuteAsync(sqlEmp, new { OrdenProduccionID = ordenId, e.EmpleadoID, e.HorasReales, e.Notas });
        }

        return ordenId;
    }

    public async Task<IEnumerable<OrdenProduccionResumen>> ListarAsync(int? centroCostoId, string? estado)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT op.OrdenProduccionID, op.CodigoOP, e.Nombre AS Estado, a.Nombre AS Producto,
                   cc.Nombre AS CentroCosto, op.CantidadProgramada, op.CantidadProducidaReal,
                   op.FechaPlanificada, op.FechaInicio, op.FechaFin, op.CostoUnitarioReal,
                   tp.Nombre AS TipoProduccion
            FROM Produccion.OrdenesProduccion op
            JOIN Produccion.EstadosOP e ON e.EstadoOPID = op.EstadoOPID
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = op.ProductoTerminadoID
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = op.CentroCostoDestinoID
            JOIN Produccion.TiposProduccion tp ON tp.TipoProduccionID = op.TipoProduccionID
            WHERE (@CentroCostoId IS NULL OR op.CentroCostoDestinoID = @CentroCostoId)
              AND (@Estado IS NULL OR e.Nombre = @Estado)
              AND op.FechaCreacion >= DATEADD(MONTH, -6, SYSUTCDATETIME())
            ORDER BY op.FechaCreacion DESC
            OFFSET 0 ROWS FETCH NEXT 500 ROWS ONLY";

        return await connection.QueryAsync<OrdenProduccionResumen>(sql, new { CentroCostoId = centroCostoId, Estado = estado });
    }

    public async Task<OrdenProduccionResumen?> ObtenerAsync(int ordenProduccionId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT op.OrdenProduccionID, op.CodigoOP, e.Nombre AS Estado, a.Nombre AS Producto,
                   cc.Nombre AS CentroCosto, op.CantidadProgramada, op.CantidadProducidaReal,
                   op.FechaPlanificada, op.FechaInicio, op.FechaFin, op.CostoUnitarioReal,
                   tp.Nombre AS TipoProduccion
            FROM Produccion.OrdenesProduccion op
            JOIN Produccion.EstadosOP e ON e.EstadoOPID = op.EstadoOPID
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = op.ProductoTerminadoID
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = op.CentroCostoDestinoID
            JOIN Produccion.TiposProduccion tp ON tp.TipoProduccionID = op.TipoProduccionID
            WHERE op.OrdenProduccionID = @OrdenProduccionId";

        return await connection.QuerySingleOrDefaultAsync<OrdenProduccionResumen>(sql, new { OrdenProduccionId = ordenProduccionId });
    }

    public async Task<OrdenProduccionDetalleEdicion?> ObtenerParaEdicionAsync(int ordenProduccionId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT op.OrdenProduccionID, op.CodigoOP, e.Nombre AS Estado,
                   op.TipoProduccionID, op.ProductoTerminadoID, op.RecetaID,
                   op.CantidadProgramada, op.ClienteID, op.CentroCostoDestinoID,
                   op.BodegaOrigenMPID, op.BodegaDestinoPTID, op.CentroTrabajoID,
                   op.FechaPlanificada, op.Observaciones,
                   c.Nombre  AS ClienteNombre,
                   t.Nombre  AS ProductoNombre,
                   t.Referencia AS ReferenciaProducto
            FROM Produccion.OrdenesProduccion op
            JOIN Produccion.EstadosOP e ON e.EstadoOPID = op.EstadoOPID
            LEFT JOIN Crm.Clientes c ON c.ClienteID = op.ClienteID
            JOIN Catalogo.Tarjetas t ON t.ArticuloID = op.ProductoTerminadoID
            WHERE op.OrdenProduccionID = @OrdenProduccionId";

        return await connection.QuerySingleOrDefaultAsync<OrdenProduccionDetalleEdicion>(
            sql, new { OrdenProduccionId = ordenProduccionId });
    }

    public async Task ActualizarAsync(int ordenProduccionId, ActualizarOrdenProduccionRequest r)
    {
        using var connection = _db.CreateConnection();
        var parametros = new DynamicParameters();
        parametros.Add("OrdenProduccionID", ordenProduccionId);
        parametros.Add("TipoProduccionID", r.TipoProduccionID);
        parametros.Add("ProductoTerminadoID", r.ProductoTerminadoID);
        parametros.Add("RecetaID", r.RecetaID);
        parametros.Add("CantidadProgramada", r.CantidadProgramada);
        parametros.Add("ClienteID", r.ClienteID);
        parametros.Add("CentroCostoDestinoID", r.CentroCostoDestinoID);
        parametros.Add("BodegaOrigenMPID", r.BodegaOrigenMPID);
        parametros.Add("BodegaDestinoPTID", r.BodegaDestinoPTID);
        parametros.Add("CentroTrabajoID", r.CentroTrabajoID);
        parametros.Add("FechaPlanificada", r.FechaPlanificada);
        parametros.Add("Observaciones", r.Observaciones);

        await connection.ExecuteAsync(
            "Produccion.sp_ActualizarOrdenProduccion", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task CancelarAsync(int ordenProduccionId)
    {
        using var connection = _db.CreateConnection();
        var parametros = new DynamicParameters();
        parametros.Add("OrdenProduccionID", ordenProduccionId);

        await connection.ExecuteAsync(
            "Produccion.sp_CancelarOrdenProduccion", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task LiberarAsync(int ordenProduccionId, int usuarioId)
    {
        using var connection = _db.CreateConnection();
        var parametros = new DynamicParameters();
        parametros.Add("OrdenProduccionID", ordenProduccionId);
        parametros.Add("UsuarioID", usuarioId);

        await connection.ExecuteAsync(
            "Produccion.sp_LiberarOrdenProduccion", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task IniciarAsync(int ordenProduccionId, int usuarioId)
    {
        using var connection = _db.CreateConnection();
        var parametros = new DynamicParameters();
        parametros.Add("OrdenProduccionID", ordenProduccionId);
        parametros.Add("UsuarioID", usuarioId);

        await connection.ExecuteAsync(
            "Produccion.sp_IniciarOrdenProduccion", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task<(decimal, int)> CerrarAsync(int ordenProduccionId, CerrarOrdenProduccionRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();
        var parametros = new DynamicParameters();
        parametros.Add("OrdenProduccionID", ordenProduccionId);
        parametros.Add("CantidadProducidaReal", r.CantidadProducidaReal);
        parametros.Add("HorasManoObra", r.HorasManoObra);
        parametros.Add("HorasCIF", r.HorasCIF);
        parametros.Add("NumeroLotePT", r.NumeroLotePT);
        parametros.Add("FechaVencimientoPT", r.FechaVencimientoPT);
        parametros.Add("UsuarioID", usuarioId);

        var resultado = await connection.QuerySingleAsync<CerrarResultado>(
            "Produccion.sp_CerrarOrdenProduccion", parametros, commandType: CommandType.StoredProcedure);

        // Entrada del PT en Visions para los artículos mapeados.
        // Los insumos ya fueron descontados del inventario al registrar cada consumo (sp_IniciarConsumo),
        // no se generan eventos de consumo aquí — CONSUMO_INSUMO no está en el CHECK de EventosSalientes.
        await connection.ExecuteAsync(@"
            INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
            SELECT 'ENTRADA_PRODUCTO_TERMINADO', ma.CentroCostoID, op.ProductoTerminadoID,
                   @Cantidad, @CostoUnitario
            FROM Produccion.OrdenesProduccion op
            JOIN Integracion.MapeoArticulos ma ON ma.ArticuloID = op.ProductoTerminadoID AND ma.Estado = 1
            JOIN Organizacion.CentrosCosto cc  ON cc.CentroCostoID = ma.CentroCostoID AND cc.TieneVisions = 1
            WHERE op.OrdenProduccionID = @OrdenProduccionID",
            new { OrdenProduccionID = ordenProduccionId, Cantidad = r.CantidadProducidaReal, CostoUnitario = resultado.CostoUnitarioReal });

        // Notificar al cliente si la orden tiene ClienteID y email
        var dbFactory = _db;
        _ = Task.Run(async () =>
        {
            try
            {
                using var emailConn = dbFactory.CreateConnection();
                var info = await emailConn.QueryFirstOrDefaultAsync<(string? Email, string Nombre, string? Descripcion, string Empresa)>(@"
                    SELECT cl.Email, cl.Nombre, op.Descripcion,
                           ISNULL((SELECT TOP 1 NombreEmpresa FROM Organizacion.ConfiguracionEmpresa), 'NEXO ERP') AS Empresa
                    FROM Produccion.OrdenesProduccion op
                    LEFT JOIN Crm.Clientes cl ON cl.ClienteID = op.ClienteID
                    WHERE op.OrdenProduccionID = @ID AND cl.Email IS NOT NULL",
                    new { ID = ordenProduccionId });

                if (info.Email is not null)
                {
                    var html = EmailTemplates.OrdenProduccionLista(info.Empresa, info.Nombre, ordenProduccionId, info.Descripcion);
                    await _email.SendAsync(new EmailMessage(info.Email,
                        $"Tu orden #{ordenProduccionId} está lista", html, info.Nombre));
                }
            }
            catch { /* fire-and-forget: no bloquear el cierre de la orden */ }
        });

        return (resultado.CostoUnitarioReal, resultado.LoteProductoTerminadoID);
    }

    public async Task AjustarConsumoAsync(long consumoId, AjustarConsumoRealRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();
        var parametros = new DynamicParameters();
        parametros.Add("ConsumoID", consumoId);
        parametros.Add("CantidadReal", r.CantidadReal);
        parametros.Add("MotivoExcesoID", r.MotivoExcesoID);
        parametros.Add("Observacion", r.Observacion);
        parametros.Add("UsuarioID", usuarioId);

        await connection.ExecuteAsync(
            "Produccion.sp_AjustarConsumoReal", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<ConsumoOpItem>> ListarConsumosAsync(int ordenProduccionId)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT c.ConsumoID, c.ArticuloID, a.Nombre AS Articulo,
                   c.CantidadTeorica, c.CantidadReal,
                   me.Nombre AS MotivoExceso, c.Observacion
            FROM Produccion.OrdenesProduccionConsumo c
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = c.ArticuloID
            LEFT JOIN Produccion.MotivosExcesoConsumo me ON me.MotivoExcesoID = c.MotivoExcesoID
            WHERE c.OrdenProduccionID = @OrdenProduccionId
            ORDER BY c.ConsumoID";
        return await connection.QueryAsync<ConsumoOpItem>(sql, new { OrdenProduccionId = ordenProduccionId });
    }

    public async Task<IEnumerable<MotivoExcesoItem>> ListarMotivosExcesoAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<MotivoExcesoItem>(
            "SELECT MotivoExcesoID, Nombre FROM Produccion.MotivosExcesoConsumo ORDER BY Nombre");
    }

    public async Task<IEnumerable<TipoProduccionItem>> ListarTiposProduccionAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<TipoProduccionItem>(
            "SELECT TipoProduccionID, Codigo, Nombre FROM Produccion.TiposProduccion ORDER BY TipoProduccionID");
    }

    public async Task<IEnumerable<StockLineaItem>> VerificarStockOrdenAsync(int ordenProduccionId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<StockLineaItem>(@"
            SELECT
                t.Nombre                                            AS Articulo,
                ISNULL(t.PresentacionCodigo, '')                    AS Unidad,
                ISNULL(CAST(
                    rd.CantidadRequerida
                    * op.CantidadProgramada
                    / NULLIF(r.CantidadRendimientoBase, 0)
                    * (1 + rd.PorcentajeMermaEstandar / 100.0)
                AS DECIMAL(18,4)), 0)                               AS CantidadRequerida,
                ISNULL(SUM(s.CantidadActual), 0)                    AS StockDisponible
            FROM Produccion.OrdenesProduccion op
            JOIN Produccion.RecetaBOM r          ON r.RecetaID   = op.RecetaID
            JOIN Produccion.RecetaBOM_Detalle rd  ON rd.RecetaID  = r.RecetaID
            JOIN Catalogo.Tarjetas t              ON t.ArticuloID = rd.InsumoID
            LEFT JOIN Inventario.InventarioStock s ON s.ArticuloID = rd.InsumoID
                                                  AND s.BodegaID   = op.BodegaOrigenMPID
            WHERE op.OrdenProduccionID = @ordenProduccionId
            GROUP BY t.Nombre, t.PresentacionCodigo,
                     rd.CantidadRequerida, op.CantidadProgramada,
                     r.CantidadRendimientoBase, rd.PorcentajeMermaEstandar
            ORDER BY t.Nombre",
            new { ordenProduccionId });
    }

    public async Task<string> GenerarSiguienteCodigoOPAsync(string prefijo)
    {
        var mesActual = NexoApi.Common.ColombiaClock.Today.ToString("yyyyMM");
        var patron    = $"{prefijo}-{mesActual}-%";
        using var connection = _db.CreateConnection();
        var usados = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Produccion.OrdenesProduccion WHERE CodigoOP LIKE @Patron",
            new { Patron = patron });
        return $"{prefijo}-{mesActual}-{(usados + 1):D3}";
    }

    public async Task<IEnumerable<FaltanteMaterialItem>> ListarFaltantesMaterialesAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<FaltanteMaterialItem>(@"
            SELECT
                op.OrdenProduccionID,
                op.CodigoOP,
                pt.Nombre  AS Producto,
                e.Nombre   AS Estado,
                t.Nombre   AS Insumo,
                ISNULL(pres.Presentacion, t.PresentacionCodigo) AS Unidad,
                CAST(ISNULL(
                    rd.CantidadRequerida * op.CantidadProgramada
                    / NULLIF(r.CantidadRendimientoBase, 0)
                    * (1 + rd.PorcentajeMermaEstandar / 100.0), 0) AS DECIMAL(18,4)) AS CantidadRequerida,
                ISNULL(SUM(s.CantidadActual), 0) AS StockDisponible,
                CASE WHEN ISNULL(SUM(s.CantidadActual), 0) <
                          CAST(ISNULL(rd.CantidadRequerida * op.CantidadProgramada
                          / NULLIF(r.CantidadRendimientoBase, 0)
                          * (1 + rd.PorcentajeMermaEstandar / 100.0), 0) AS DECIMAL(18,4))
                     THEN CAST(ISNULL(rd.CantidadRequerida * op.CantidadProgramada
                          / NULLIF(r.CantidadRendimientoBase, 0)
                          * (1 + rd.PorcentajeMermaEstandar / 100.0), 0) AS DECIMAL(18,4))
                          - ISNULL(SUM(s.CantidadActual), 0)
                     ELSE 0 END AS Faltante
            FROM Produccion.OrdenesProduccion op
            JOIN Produccion.EstadosOP e          ON e.EstadoOPID   = op.EstadoOPID
            JOIN Catalogo.Tarjetas pt            ON pt.ArticuloID  = op.ProductoTerminadoID
            JOIN Produccion.RecetaBOM r          ON r.RecetaID     = op.RecetaID
            JOIN Produccion.RecetaBOM_Detalle rd  ON rd.RecetaID   = r.RecetaID
            JOIN Catalogo.Tarjetas t             ON t.ArticuloID   = rd.InsumoID
            LEFT JOIN Inventario.InventarioStock s ON s.ArticuloID = rd.InsumoID
                                                  AND s.BodegaID   = op.BodegaOrigenMPID
            LEFT JOIN Catalogo.Presentacion pres  ON pres.Codigo   = t.PresentacionCodigo
            WHERE e.Nombre IN ('Planificada', 'En Proceso', 'Retrasada')
            GROUP BY op.OrdenProduccionID, op.CodigoOP, pt.Nombre, e.Nombre,
                     t.Nombre, t.PresentacionCodigo, pres.Presentacion,
                     rd.CantidadRequerida, rd.PorcentajeMermaEstandar,
                     op.CantidadProgramada, r.CantidadRendimientoBase
            HAVING ISNULL(SUM(s.CantidadActual), 0) <
                   CAST(ISNULL(rd.CantidadRequerida * op.CantidadProgramada
                   / NULLIF(r.CantidadRendimientoBase, 0)
                   * (1 + rd.PorcentajeMermaEstandar / 100.0), 0) AS DECIMAL(18,4))
            ORDER BY op.CodigoOP, Faltante DESC");
    }

    public async Task<IEnumerable<DesviacionConsumoItem>> ListarDesviacionesConsumoAsync(DateTime? desde, DateTime? hasta)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<DesviacionConsumoItem>(@"
            SELECT
                op.OrdenProduccionID,
                op.CodigoOP,
                pt.Nombre  AS Producto,
                op.FechaFin,
                t.Nombre   AS Insumo,
                ISNULL(pres.Presentacion, t.PresentacionCodigo) AS Unidad,
                c.CantidadTeorica,
                c.CantidadReal,
                c.CantidadReal - c.CantidadTeorica AS Desviacion
            FROM Produccion.OrdenesProduccion op
            JOIN Produccion.EstadosOP e                    ON e.EstadoOPID      = op.EstadoOPID
            JOIN Catalogo.Tarjetas pt                      ON pt.ArticuloID     = op.ProductoTerminadoID
            JOIN Produccion.OrdenesProduccionConsumo c     ON c.OrdenProduccionID = op.OrdenProduccionID
            JOIN Catalogo.Tarjetas t                       ON t.ArticuloID      = c.ArticuloID
            LEFT JOIN Catalogo.Presentacion pres           ON pres.Codigo       = t.PresentacionCodigo
            WHERE e.Nombre = 'Finalizada'
              AND (@Desde IS NULL OR op.FechaFin >= @Desde)
              AND (@Hasta IS NULL OR op.FechaFin < DATEADD(day, 1, @Hasta))
            ORDER BY op.FechaFin DESC, op.CodigoOP, t.Nombre",
            new { Desde = desde, Hasta = hasta });
    }

    public async Task<string> GenerarSiguienteNumeroLoteAsync(string prefijo)
    {
        var mesActual = NexoApi.Common.ColombiaClock.Today.ToString("yyyyMM");
        var patron    = $"{prefijo}-{mesActual}-%";
        using var connection = _db.CreateConnection();
        var usados = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Inventario.Lotes WHERE NumeroLote LIKE @Patron",
            new { Patron = patron });
        return $"{prefijo}-{mesActual}-{(usados + 1):D3}";
    }

    private record OpRetrasadaInfo(string CodigoOP, string ProductoNombre, DateTime FechaPlanificada);

    public async Task<int> MarcarRetrasadasAsync()
    {
        using var connection = _db.CreateConnection();
        var estadoId = await connection.ExecuteScalarAsync<int?>(
            "SELECT EstadoOPID FROM Produccion.EstadosOP WHERE Nombre = 'Retrasada'");
        if (estadoId is null) return 0;

        // 1. SELECT the OPs that WILL be updated (same WHERE clause)
        var opsAfectadas = (await connection.QueryAsync<OpRetrasadaInfo>(@"
            SELECT op.CodigoOP, t.Nombre AS ProductoNombre, op.FechaPlanificada
            FROM Produccion.OrdenesProduccion op
            JOIN Catalogo.Tarjetas t ON t.ArticuloID = op.ProductoTerminadoID
            WHERE op.FechaPlanificada < CAST(GETDATE() AS DATE)
              AND op.EstadoOPID IN (
                  SELECT EstadoOPID FROM Produccion.EstadosOP
                  WHERE Nombre IN ('En Proceso', 'Planificada')
              )")).ToList();

        if (opsAfectadas.Count == 0) return 0;

        // 2. Execute the UPDATE
        var affected = await connection.ExecuteAsync(@"
            UPDATE Produccion.OrdenesProduccion
            SET EstadoOPID = @EstadoId
            WHERE FechaPlanificada < CAST(GETDATE() AS DATE)
              AND EstadoOPID IN (
                  SELECT EstadoOPID FROM Produccion.EstadosOP
                  WHERE Nombre IN ('En Proceso', 'Planificada')
              )",
            new { EstadoId = estadoId.Value });

        // 3. Send summary email to production managers — fire-and-forget, never blocks the return value
        // Uses its own connection to avoid use-after-dispose of the outer using block.
        var dbFactory = _db;
        _ = Task.Run(async () =>
        {
            try
            {
                using var emailConn = dbFactory.CreateConnection();
                var destinatarios = (await emailConn.QueryAsync<string>(@"
                    SELECT u.Email
                    FROM Seguridad.Usuarios u
                    JOIN Seguridad.Roles r ON r.RolID = u.RolID
                    WHERE r.Nombre IN ('Administracion', 'Jefes')
                      AND u.Email IS NOT NULL
                      AND u.Estado = 1")).ToList();

                if (destinatarios.Count == 0) return;

                var subject = $"NEXO — {opsAfectadas.Count} OP(s) marcadas como Retrasada";

                var filas = string.Join("\n", opsAfectadas.Select(op =>
                    $"  • {op.CodigoOP} — {op.ProductoNombre} — {op.FechaPlanificada:dd/MM/yyyy}"));

                var html = $@"<pre style=""font-family:monospace;font-size:14px"">
Las siguientes órdenes de producción fueron marcadas automáticamente como <b>Retrasada</b>
porque su fecha planificada ya venció:

{filas}

Total: {opsAfectadas.Count} OP(s)
</pre>";

                foreach (var email in destinatarios)
                    await _email.SendAsync(new EmailMessage(email, subject, html));
            }
            catch { /* no bloquear el resultado de MarcarRetrasadasAsync */ }
        });

        return affected;
    }

    public async Task<IEnumerable<SubproductoRecetaItem>> ListarSubproductosRecetaAsync(int recetaId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<SubproductoRecetaItem>(@"
            SELECT s.SubproductoBOMID, s.RecetaID,
                   s.SubproductoID, t.Nombre AS Subproducto, t.Referencia,
                   s.CantidadPorLoteBase,
                   ISNULL(pres.Presentacion, t.PresentacionCodigo) AS Unidad,
                   s.BodegaDestinoID, b.Nombre AS BodegaDestino, s.Activo
            FROM Produccion.RecetaBOM_Subproducto s
            JOIN Catalogo.Tarjetas t ON t.ArticuloID = s.SubproductoID
            LEFT JOIN Catalogo.Presentacion pres ON pres.Codigo = t.PresentacionCodigo
            LEFT JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaDestinoID
            WHERE s.RecetaID = @RecetaID
            ORDER BY s.SubproductoBOMID",
            new { RecetaID = recetaId });
    }

    public async Task<int> AgregarSubproductoRecetaAsync(int recetaId, AgregarSubproductoRecetaRequest r)
    {
        using var conn = _db.CreateConnection();
        var existe = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Produccion.RecetaBOM_Subproducto WHERE RecetaID = @RecetaID AND SubproductoID = @SubproductoID",
            new { RecetaID = recetaId, r.SubproductoID });
        if (existe > 0)
            throw new InvalidOperationException("El artículo ya existe como subproducto de esta receta.");
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Produccion.RecetaBOM_Subproducto
                (RecetaID, SubproductoID, CantidadPorLoteBase, BodegaDestinoID, Activo)
            VALUES (@RecetaID, @SubproductoID, @CantidadPorLoteBase, @BodegaDestinoID, 1);
            SELECT SCOPE_IDENTITY();",
            new { RecetaID = recetaId, r.SubproductoID, r.CantidadPorLoteBase, r.BodegaDestinoID });
    }

    public async Task EliminarSubproductoRecetaAsync(int subproductoBomId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM Produccion.RecetaBOM_Subproducto WHERE SubproductoBOMID = @ID",
            new { ID = subproductoBomId });
    }

    public async Task<IEnumerable<SubproductoOpItem>> ListarSubproductosOpAsync(int ordenProduccionId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<SubproductoOpItem>(@"
            SELECT s.SubproductoOPID, s.OrdenProduccionID,
                   s.SubproductoID, t.Nombre AS Subproducto, t.Referencia,
                   s.CantidadGenerada,
                   ISNULL(pres.Presentacion, t.PresentacionCodigo) AS Unidad,
                   s.LoteID, s.KardexID, s.FechaGeneracion
            FROM Produccion.OrdenProduccionSubproducto s
            JOIN Catalogo.Tarjetas t ON t.ArticuloID = s.SubproductoID
            LEFT JOIN Catalogo.Presentacion pres ON pres.Codigo = t.PresentacionCodigo
            WHERE s.OrdenProduccionID = @OrdenProduccionID
            ORDER BY s.SubproductoOPID",
            new { OrdenProduccionID = ordenProduccionId });
    }
}