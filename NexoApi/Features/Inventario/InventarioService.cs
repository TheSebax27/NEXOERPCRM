using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using NexoApi.Common.Data;
using NexoApi.Features.Inventario.Dtos;

namespace NexoApi.Features.Inventario;

public interface IInventarioService
{
    Task<IEnumerable<StockConsolidadoItem>> ConsultarStockAsync(int? centroCostoId, int? bodegaId, string? filtro, int? articuloId = null);
    Task<RegistrarBajaResponse> RegistrarBajaAsync(RegistrarBajaRequest request, int usuarioId);
    Task<AjustarInventarioResponse> AjustarInventarioAsync(AjustarInventarioRequest request, int usuarioId);
    Task<IEnumerable<MotivoPerdidaItem>> ListarMotivosPerdidaAsync();
    Task<IEnumerable<KardexMovimientoItem>> ConsultarKardexAsync(int? articuloId, int? bodegaId, DateTime? desde, DateTime? hasta);
    Task<IEnumerable<LoteProximoVencerItem>> ListarLotesPorVencerAsync(int diasAlerta);
    Task<IEnumerable<OCLineSugeridaItem>> SugerirOCAsync();
    Task<ResumenMermasResponse> ListarMermasAsync(DateTime? desde, DateTime? hasta, string? tipo);
}

public class InventarioService : IInventarioService
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<InventarioService> _logger;

    public InventarioService(IDbConnectionFactory db, ILogger<InventarioService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<StockConsolidadoItem>> ConsultarStockAsync(int? centroCostoId, int? bodegaId, string? filtro, int? articuloId = null)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            WITH ConsumoUltimos30 AS (
                SELECT ArticuloID,
                       CAST(SUM(ABS(Cantidad)) / 30.0 AS decimal(18,6)) AS ConsumoDiarioProm
                FROM Kardex.KardexMovimientos
                WHERE Cantidad < 0
                  AND Fecha >= DATEADD(day, -30, GETDATE())
                GROUP BY ArticuloID
            )
            SELECT s.ArticuloID, s.SKU, s.Articulo, s.TipoArticulo, s.Unidad, s.UnidadesPorEmbalaje, s.BodegaID, s.Bodega,
                   s.CentroCostoID, s.CentroCosto, s.LoteID, s.NumeroLote, s.FechaVencimiento,
                   s.CantidadActual, s.CostoUnitarioLote, s.ValorTotal, s.RequierePedido,
                   CAST(CASE WHEN a.Imagen IS NULL THEN 0 ELSE 1 END AS BIT) AS TieneImagen,
                   ISNULL(a.StockMinimo, 0) AS StockMinimo,
                   ISNULL(a.CostoPromedio, 0) AS CostoPromedio,
                   ISNULL(a.PPublico, 0) AS PrecioVenta,
                   c.ConsumoDiarioProm,
                   CAST(NULL AS int) AS DiasAgotamiento,
                   ISNULL(pres.Presentacion, s.Unidad) AS PresentacionNombre
            FROM Inventario.vw_StockConsolidado s
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = s.ArticuloID
            LEFT JOIN ConsumoUltimos30 c ON c.ArticuloID = s.ArticuloID
            LEFT JOIN Catalogo.Presentacion pres ON pres.Codigo = s.Unidad
            WHERE (@CentroCostoId IS NULL OR s.CentroCostoID = @CentroCostoId)
              AND (@BodegaId IS NULL OR s.BodegaID = @BodegaId)
              AND (@ArticuloId IS NULL OR s.ArticuloID = @ArticuloId)
              AND (@Filtro IS NULL OR s.SKU LIKE '%' + @Filtro + '%' OR s.Articulo LIKE '%' + @Filtro + '%')
            ORDER BY s.Articulo, s.Bodega";

        return await connection.QueryAsync<StockConsolidadoItem>(sql, new
        {
            CentroCostoId = centroCostoId,
            BodegaId = bodegaId,
            Filtro = filtro,
            ArticuloId = articuloId
        });
    }

    public async Task<RegistrarBajaResponse> RegistrarBajaAsync(RegistrarBajaRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        var parametros = new DynamicParameters();
        parametros.Add("ArticuloID", r.ArticuloID);
        parametros.Add("BodegaID", r.BodegaID);
        parametros.Add("LoteID", r.LoteID);
        parametros.Add("CantidadPerdida", r.CantidadPerdida);
        parametros.Add("MotivoID", r.MotivoID);
        parametros.Add("ObservacionDetallada", r.ObservacionDetallada);
        parametros.Add("UsuarioRegistraID", usuarioId);
        if (r.Fecha.HasValue) parametros.Add("Fecha", r.Fecha.Value);

        var resultado = await connection.QuerySingleAsync<RegistrarBajaResponse>(
            "Kardex.sp_RegistrarBajaInventario",
            parametros,
            commandType: CommandType.StoredProcedure);

        // Replicar baja en Visions para todos los CC mapeados (Cantidad negativa = descuento).
        try
        {
            await connection.ExecuteAsync(@"
                INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
                SELECT 'BAJA_INVENTARIO', ma.CentroCostoID, ma.ArticuloID, -@Cantidad, ISNULL(a.CostoPromedio, 0)
                FROM Integracion.MapeoArticulos ma
                JOIN Catalogo.Tarjetas a          ON a.ArticuloID    = ma.ArticuloID
                JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
                WHERE ma.ArticuloID = @ArticuloId AND ma.Estado = 1 AND cc.TieneVisions = 1",
                new { ArticuloId = r.ArticuloID, Cantidad = r.CantidadPerdida });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SYNC_DRIFT: EventosSalientes BAJA_INVENTARIO falló para ArticuloID={ArticuloId} Cantidad={Cantidad}. Stock en Kardex ya descontado.", r.ArticuloID, r.CantidadPerdida);
        }

        return resultado;
    }

    public async Task<AjustarInventarioResponse> AjustarInventarioAsync(AjustarInventarioRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        var parametros = new DynamicParameters();
        parametros.Add("ArticuloID", r.ArticuloID);
        parametros.Add("BodegaID", r.BodegaID);
        parametros.Add("Cantidad", r.Cantidad);
        parametros.Add("CostoUnitario", r.CostoUnitario);
        parametros.Add("Motivo", r.Motivo);
        parametros.Add("UsuarioID", usuarioId);
        if (r.Fecha.HasValue) parametros.Add("Fecha", r.Fecha.Value);

        var resultado = await connection.QuerySingleAsync<AjustarInventarioResponse>(
            "Kardex.sp_AjustePositivoInventario",
            parametros,
            commandType: CommandType.StoredProcedure);

        // Replicar ajuste en Visions para todos los CC mapeados.
        try
        {
            await connection.ExecuteAsync(@"
                INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
                SELECT 'AJUSTE_INVENTARIO', ma.CentroCostoID, ma.ArticuloID, @Cantidad, @CostoUnitario
                FROM Integracion.MapeoArticulos ma
                JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
                WHERE ma.ArticuloID = @ArticuloId AND ma.Estado = 1 AND cc.TieneVisions = 1",
                new { ArticuloId = r.ArticuloID, r.Cantidad, CostoUnitario = r.CostoUnitario });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SYNC_DRIFT: EventosSalientes AJUSTE_INVENTARIO falló para ArticuloID={ArticuloId} Cantidad={Cantidad}. Stock en Kardex ya ajustado.", r.ArticuloID, r.Cantidad);
        }

        return resultado;
    }

    public async Task<IEnumerable<MotivoPerdidaItem>> ListarMotivosPerdidaAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<MotivoPerdidaItem>(
            "SELECT MotivoID, Nombre FROM Kardex.TiposMotivoLoss ORDER BY Nombre");
    }

    public async Task<IEnumerable<KardexMovimientoItem>> ConsultarKardexAsync(
        int? articuloId, int? bodegaId, DateTime? desde, DateTime? hasta)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT TOP 500
                k.KardexID, k.Fecha,
                a.Referencia AS SKU, a.Nombre AS Articulo,
                a.PresentacionCodigo AS Unidad, p.Fracciones AS UnidadesPorEmbalaje,
                b.Nombre AS Bodega,
                tm.Nombre AS TipoMovimiento,
                k.Cantidad, k.CostoUnitario,
                k.CantidadSaldo,
                ABS(k.Cantidad) * k.CostoUnitario AS ValorMovimiento,
                k.ObservacionDetallada,
                l.NumeroLote, l.FechaVencimiento
            FROM Kardex.KardexMovimientos k
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = k.ArticuloID
            LEFT JOIN Catalogo.Presentacion p ON p.Codigo = a.PresentacionCodigo
            JOIN Inventario.Bodegas b ON b.BodegaID = k.BodegaID
            JOIN Kardex.TiposMovimientoKardex tm ON tm.TipoMovID = k.TipoMovID
            LEFT JOIN Inventario.Lotes l ON l.LoteID = k.LoteID
            WHERE (@ArticuloId IS NULL OR k.ArticuloID = @ArticuloId)
              AND (@BodegaId IS NULL OR k.BodegaID = @BodegaId)
              AND (@Desde IS NULL OR k.Fecha >= @Desde)
              AND (@Hasta IS NULL OR k.Fecha < DATEADD(DAY,1,@Hasta))
            ORDER BY k.Fecha DESC, k.KardexID DESC";
        return await connection.QueryAsync<KardexMovimientoItem>(sql,
            new { ArticuloId = articuloId, BodegaId = bodegaId, Desde = desde, Hasta = hasta });
    }

    public async Task<IEnumerable<LoteProximoVencerItem>> ListarLotesPorVencerAsync(int diasAlerta)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT a.Referencia AS SKU, a.Nombre AS Articulo, b.Nombre AS Bodega,
                   l.NumeroLote, l.FechaVencimiento,
                   s.CantidadActual,
                   DATEDIFF(DAY, GETDATE(), l.FechaVencimiento) AS DiasParaVencer
            FROM Inventario.vw_StockConsolidado s
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = s.ArticuloID
            JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaID
            JOIN Inventario.Lotes l ON l.LoteID = s.LoteID
            WHERE s.CantidadActual > 0
              AND l.FechaVencimiento IS NOT NULL
              AND l.FechaVencimiento <= DATEADD(DAY, @DiasAlerta, GETDATE())
            ORDER BY l.FechaVencimiento ASC";
        return await connection.QueryAsync<LoteProximoVencerItem>(sql, new { DiasAlerta = diasAlerta });
    }

    public async Task<IEnumerable<OCLineSugeridaItem>> SugerirOCAsync()
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            WITH Consumo AS (
                SELECT ArticuloID,
                       CAST(SUM(ABS(Cantidad)) / 30.0 AS decimal(18,6)) AS ConsumoDiario
                FROM Kardex.KardexMovimientos
                WHERE Cantidad < 0
                  AND Fecha >= DATEADD(day, -30, GETDATE())
                GROUP BY ArticuloID
            ),
            StockTotal AS (
                SELECT ArticuloID, SUM(CantidadActual) AS Stock
                FROM Inventario.vw_StockConsolidado
                GROUP BY ArticuloID
            )
            SELECT
                a.ArticuloID,
                a.Referencia AS SKU,
                a.Nombre AS Articulo,
                ta.Nombre AS TipoArticulo,
                ISNULL(st.Stock, 0) AS StockActual,
                ISNULL(a.StockMinimo, 0) AS StockMinimo,
                ISNULL(c.ConsumoDiario, 0) AS ConsumoDiarioProm,
                CAST(CEILING(
                    CASE
                        WHEN ISNULL(c.ConsumoDiario, 0) > 0
                        THEN CASE
                            WHEN c.ConsumoDiario * 30 > (ISNULL(a.StockMinimo, 0) - ISNULL(st.Stock, 0))
                            THEN c.ConsumoDiario * 30
                            ELSE (ISNULL(a.StockMinimo, 0) - ISNULL(st.Stock, 0))
                        END
                        ELSE (ISNULL(a.StockMinimo, 0) - ISNULL(st.Stock, 0))
                    END
                ) AS decimal(18,4)) AS CantidadSugerida,
                ISNULL(a.CostoPromedio, 0) AS CostoPromedio
            FROM Catalogo.Tarjetas a
            JOIN Catalogo.TiposArticulo ta ON ta.TipoArticuloID = a.TipoArticuloID
            LEFT JOIN StockTotal st ON st.ArticuloID = a.ArticuloID
            LEFT JOIN Consumo c ON c.ArticuloID = a.ArticuloID
            WHERE a.StockMinimo > 0
              AND ISNULL(st.Stock, 0) <= a.StockMinimo
              AND a.TipoArticuloID NOT IN (
                  SELECT TipoArticuloID FROM Catalogo.TiposArticulo WHERE Nombre = 'Producto Terminado'
              )
            ORDER BY a.Nombre";
        return await connection.QueryAsync<OCLineSugeridaItem>(sql);
    }

    public async Task<ResumenMermasResponse> ListarMermasAsync(DateTime? desde, DateTime? hasta, string? tipo)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT src.TipoMerma, src.Fecha, src.OrdenOP,
                   src.ArticuloID, src.Articulo, src.Referencia, src.Unidad,
                   src.Cantidad, src.CostoUnitario, src.ValorPerdido,
                   src.Motivo, src.Observacion
            FROM (
                -- Fuente 1: Bajas de inventario
                SELECT
                    'Baja Inventario'                          AS TipoMerma,
                    b.Fecha,
                    CAST(NULL AS NVARCHAR(30))                 AS OrdenOP,
                    a.ArticuloID,
                    a.Nombre                                   AS Articulo,
                    a.Referencia,
                    a.PresentacionCodigo                       AS Unidad,
                    b.CantidadPerdida                          AS Cantidad,
                    b.CostoUnitario,
                    b.CostoTotal                               AS ValorPerdido,
                    m.Nombre                                   AS Motivo,
                    b.ObservacionDetallada                     AS Observacion
                FROM Kardex.BajasInventarioPerdidas b
                JOIN Catalogo.Tarjetas a  ON a.ArticuloID  = b.ArticuloID
                JOIN Kardex.TiposMotivoLoss m ON m.MotivoID = b.MotivoID
                WHERE (@Tipo IS NULL OR @Tipo = 'BAJA')

                UNION ALL

                -- Fuente 2: Exceso de consumo en OPs cerradas (merma real de proceso)
                SELECT
                    'Exceso Producción'                        AS TipoMerma,
                    op.FechaFin                                AS Fecha,
                    op.CodigoOP                                AS OrdenOP,
                    a.ArticuloID,
                    a.Nombre                                   AS Articulo,
                    a.Referencia,
                    a.PresentacionCodigo                       AS Unidad,
                    c.CantidadReal - c.CantidadTeorica         AS Cantidad,
                    ISNULL(a.Costo, 0)                         AS CostoUnitario,
                    (c.CantidadReal - c.CantidadTeorica)
                        * ISNULL(a.Costo, 0)                   AS ValorPerdido,
                    me.Nombre                                  AS Motivo,
                    c.Observacion
                FROM Produccion.OrdenesProduccionConsumo c
                JOIN Produccion.OrdenesProduccion op ON op.OrdenProduccionID = c.OrdenProduccionID
                JOIN Catalogo.Tarjetas a              ON a.ArticuloID        = c.ArticuloID
                LEFT JOIN Produccion.MotivosExcesoConsumo me ON me.MotivoExcesoID = c.MotivoExcesoID
                WHERE op.EstadoOPID = (SELECT EstadoOPID FROM Produccion.EstadosOP WHERE Nombre = 'Finalizada')
                  AND c.CantidadReal > c.CantidadTeorica
                  AND (@Tipo IS NULL OR @Tipo = 'PRODUCCION')
            ) src
            WHERE (@Desde IS NULL OR src.Fecha >= @Desde)
              AND (@Hasta IS NULL OR src.Fecha <= DATEADD(day, 1, @Hasta))
            ORDER BY src.Fecha DESC";

        var detalle = (await connection.QueryAsync<MermaItem>(sql,
            new { Desde = desde, Hasta = hasta, Tipo = tipo })).ToList();

        return new ResumenMermasResponse(
            TotalValorBajas:      detalle.Where(d => d.TipoMerma == "Baja Inventario").Sum(d => d.ValorPerdido),
            TotalValorProduccion: detalle.Where(d => d.TipoMerma == "Exceso Producción").Sum(d => d.ValorPerdido),
            TotalValor:           detalle.Sum(d => d.ValorPerdido),
            TotalEventos:         detalle.Count,
            Detalle:              detalle
        );
    }
}