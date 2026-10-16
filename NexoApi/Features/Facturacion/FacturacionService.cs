using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Facturacion.Dtos;
using NexoApi.Features.Produccion;
using NexoApi.Features.Produccion.Dtos;

namespace NexoApi.Features.Facturacion;

public interface IFacturacionService
{
    Task<FacturasPaginadasResponse> ListarFacturasAsync(int? clienteId, int? centroCostoId, string? tipDoc, string? estado, DateTime? desde, DateTime? hasta, string? texto, bool soloNoPagadas, int pagina = 1, int tamano = 100);
    Task<string?> ObtenerSiguienteNroDocAsync(string tipDoc);
    Task<int> CrearFacturaAsync(CrearFacturaRequest request, int usuarioId);
    Task EliminarFacturaAsync(int facturaId);

    Task<IEnumerable<FacturaLineaItem>> ListarLineasAsync(int facturaId);
    Task<IEnumerable<FacturaLineaStockItem>> ObtenerStockLineasAsync(int facturaId);
    Task DescontarStockAsync(int facturaId, int usuarioId);
    Task ConfirmarVisionsAsync(int facturaId);
    Task<int> ContarStockFallidoAsync();

    Task<IEnumerable<PagoItem>> ListarPagosAsync(int facturaId);
    Task<int> CrearPagoAsync(CrearPagoRequest request, int usuarioId);

    Task<List<VerificarProduccionItem>> VerificarProduccionFacturaAsync(int facturaId);
    Task<List<AutoProducirResultItem>> AutoProducirFacturaAsync(int facturaId, int usuarioId);
    Task<UltimoPrecioItem?> ObtenerUltimoPrecioAsync(int clienteId, int articuloId);
}

public class FacturacionService : IFacturacionService
{
    private readonly IDbConnectionFactory _db;
    private readonly IOrdenesProduccionService _produccion;

    public FacturacionService(IDbConnectionFactory db, IOrdenesProduccionService produccion)
    {
        _db = db;
        _produccion = produccion;
    }

    private record FacturaCruda(
        int FacturaID, int ClienteID, string Cliente, string? NitCliente,
        DateTime Fecha, string? Notas,
        string TipDoc, string? NroDoc,
        decimal Total, decimal TotalPagado,
        bool StockDescontado, bool ProduccionAutoEjecutada, bool VisionsConfirmado,
        int? CentroCostoID, string? CentroCostoNombre
    );

    // Devuelve el siguiente NroDoc segun el modo configurado en ConfiguracionEmpresa:
    // - Secuencial: incremento atomico del contador propio de NEXO
    // - Aleatorio : 10 digitos aleatorios
    // - Manual    : null (el usuario escribe el numero)
    public async Task<string?> ObtenerSiguienteNroDocAsync(string tipDoc)
    {
        using var connection = _db.CreateConnection();

        await connection.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1)
                INSERT INTO Organizacion.ConfiguracionEmpresa
                    (ConfiguracionID, NombreEmpresa, NombrePropietario, Logo, LogoContentType,
                     UsaVisions, ManejarVencimientos, DiasAlertaVencimiento, ModoLotes,
                     ModoNroDoc, UltimoNroDocSecuencial)
                VALUES
                    (1, 'NEXO ERP', NULL, NULL, NULL, 1, 0, 7, 'FIFO', 'Manual', 0)
            """);
        var modo = await connection.ExecuteScalarAsync<string>(
            "SELECT ModoNroDoc FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");

        switch (modo)
        {
            case "Secuencial":
            {
                var siguiente = await connection.ExecuteScalarAsync<long>(@"
                    UPDATE Organizacion.ConfiguracionEmpresa
                    SET UltimoNroDocSecuencial = UltimoNroDocSecuencial + 1
                    OUTPUT INSERTED.UltimoNroDocSecuencial
                    WHERE ConfiguracionID = 1");
                return siguiente.ToString();
            }

            case "Aleatorio":
            {
                return Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString();
            }

            default: // Manual
                return null;
        }
    }

    public async Task<FacturasPaginadasResponse> ListarFacturasAsync(
        int? clienteId, int? centroCostoId, string? tipDoc, string? estado, DateTime? desde, DateTime? hasta,
        string? texto, bool soloNoPagadas, int pagina = 1, int tamano = 100)
    {
        using var connection = _db.CreateConnection();
        var offset = (pagina - 1) * tamano;
        var textoBusqueda = string.IsNullOrWhiteSpace(texto) ? null : $"%{texto.Trim()}%";

        var sqlBase = @"
            FROM Facturacion.Facturas f
            JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
            LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = f.CentroCostoID
            LEFT JOIN (
                SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                FROM Facturacion.FacturaLineas GROUP BY FacturaID
            ) tot ON tot.FacturaID = f.FacturaID
            LEFT JOIN (
                SELECT FacturaID, SUM(Monto) AS TotalPagado
                FROM Facturacion.Pagos GROUP BY FacturaID
            ) pag ON pag.FacturaID = f.FacturaID
            WHERE (@ClienteId      IS NULL OR f.ClienteID      = @ClienteId)
              AND (@TipDoc         IS NULL OR f.TipDoc         = @TipDoc)
              AND (@Desde          IS NULL OR f.Fecha          >= @Desde)
              AND (@Hasta          IS NULL OR f.Fecha          <= @Hasta)
              AND (@CentroCostoId  IS NULL OR f.CentroCostoID  = @CentroCostoId)
              AND (@Texto          IS NULL OR f.NroDoc LIKE @Texto OR c.NIT LIKE @Texto OR c.Nombre LIKE @Texto)"
            + (soloNoPagadas
                ? " AND f.VisionsConfirmado = 0 AND (ISNULL(pag.TotalPagado,0) < ISNULL(tot.Total,0) OR ISNULL(tot.Total,0) = 0)"
                : "");

        var p = new { ClienteId = clienteId, CentroCostoId = centroCostoId, TipDoc = tipDoc, Desde = desde, Hasta = hasta, Texto = textoBusqueda };

        var total = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) " + sqlBase, p, commandTimeout: 30);

        var crudas = await connection.QueryAsync<FacturaCruda>(@"
            SELECT f.FacturaID, f.ClienteID, c.Nombre AS Cliente, c.NIT AS NitCliente,
                   f.Fecha, f.Notas, f.TipDoc, f.NroDoc,
                   ISNULL(ROUND(tot.Total, 0), 0) AS Total,
                   ISNULL(pag.TotalPagado, 0) AS TotalPagado,
                   f.StockDescontado, f.ProduccionAutoEjecutada, f.VisionsConfirmado,
                   f.CentroCostoID, cc.Nombre AS CentroCostoNombre" +
            sqlBase + @"
            ORDER BY f.Fecha DESC, f.FacturaID DESC
            OFFSET @Offset ROWS FETCH NEXT @Tamano ROWS ONLY",
            new { ClienteId = clienteId, CentroCostoId = centroCostoId, TipDoc = tipDoc, Desde = desde, Hasta = hasta, Texto = textoBusqueda, Offset = offset, Tamano = tamano },
            commandTimeout: 30);

        var items = crudas.Select(f =>
        {
            var saldo = f.Total - f.TotalPagado;
            var estadoCalc = f.TotalPagado <= 0 ? "PENDIENTE" : saldo > 0 ? "PARCIAL" : "PAGADA";
            return new FacturaItem(
                f.FacturaID, f.ClienteID, f.Cliente, f.NitCliente,
                f.Fecha, f.Notas, f.TipDoc, f.NroDoc,
                f.Total, f.TotalPagado, saldo, estadoCalc,
                f.StockDescontado, f.ProduccionAutoEjecutada, f.VisionsConfirmado,
                f.CentroCostoID, f.CentroCostoNombre);
        });

        if (estado is not null)
            items = items.Where(i => i.Estado == estado);

        return new FacturasPaginadasResponse(items.ToList(), total, pagina, tamano);
    }

    public async Task<int> CrearFacturaAsync(CrearFacturaRequest r, int usuarioId)
    {
        if (r.Lineas.Count == 0)
            throw new InvalidOperationException("La factura debe tener al menos una linea.");

        if (r.Lineas.Any(l => l.ArticuloID is null && l.ComboID is null && string.IsNullOrWhiteSpace(l.DescripcionLinea)))
            throw new InvalidOperationException("Cada linea debe tener un articulo, un combo o una descripcion.");

        if (r.Lineas.Any(l => l.Cantidad <= 0))
            throw new InvalidOperationException("Todas las cantidades deben ser mayores a cero.");

        if (r.Lineas.Any(l => l.PrecioUnitario < 0))
            throw new InvalidOperationException("El precio unitario no puede ser negativo.");

        if (!TiposDocumento.Todos.Contains(r.TipDoc))
            throw new InvalidOperationException($"TipDoc invalido: {r.TipDoc}.");

        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string sqlFactura = @"
                INSERT INTO Facturacion.Facturas (ClienteID, Fecha, Notas, TipDoc, NroDoc, UsuarioID, CentroCostoID)
                OUTPUT INSERTED.FacturaID
                VALUES (@ClienteID, @Fecha, @Notas, @TipDoc, @NroDoc, @UsuarioID, @CentroCostoID)";

            var facturaId = await connection.ExecuteScalarAsync<int>(sqlFactura,
                new { r.ClienteID, r.Fecha, r.Notas, r.TipDoc, r.NroDoc, UsuarioID = usuarioId, r.CentroCostoID },
                transaction);

            const string sqlLinea = @"
                INSERT INTO Facturacion.FacturaLineas (FacturaID, ArticuloID, ComboID, DescripcionLinea, Cantidad, PrecioUnitario, Nota)
                VALUES (@FacturaId, @ArticuloID, @ComboID, @DescripcionLinea, @Cantidad, @PrecioUnitario, @Nota)";

            foreach (var linea in r.Lineas)
            {
                await connection.ExecuteAsync(sqlLinea,
                    new
                    {
                        FacturaId = facturaId,
                        linea.ArticuloID, linea.ComboID, linea.DescripcionLinea,
                        linea.Cantidad, linea.PrecioUnitario, linea.Nota
                    }, transaction);
            }

            transaction.Commit();
            return facturaId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<FacturaLineaItem>> ListarLineasAsync(int facturaId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT l.LineaID, l.FacturaID, l.ArticuloID,
                   a.Referencia AS SkuArticulo,
                   COALESCE(a.Nombre, l.DescripcionLinea, c.Nombre) AS NombreArticulo,
                   l.ComboID, l.DescripcionLinea, l.Nota,
                   l.Cantidad, l.PrecioUnitario, ROUND(l.Cantidad * l.PrecioUnitario, 0) AS Subtotal,
                   a.PresentacionCodigo AS Unidad, p.Fracciones AS UnidadesPorEmbalaje
            FROM Facturacion.FacturaLineas l
            LEFT JOIN Catalogo.Tarjetas a ON a.ArticuloID = l.ArticuloID
            LEFT JOIN Marketing.Combos c ON c.ComboID = l.ComboID
            LEFT JOIN Catalogo.Presentacion p ON p.Codigo = a.PresentacionCodigo
            WHERE l.FacturaID = @FacturaId
            ORDER BY l.LineaID";

        return await connection.QueryAsync<FacturaLineaItem>(sql, new { FacturaId = facturaId });
    }

    public async Task<IEnumerable<FacturaLineaStockItem>> ObtenerStockLineasAsync(int facturaId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT fl.ArticuloID, a.Referencia AS SkuArticulo, a.Nombre AS NombreArticulo,
                   fl.Cantidad AS CantidadFacturada,
                   ISNULL((SELECT SUM(s.CantidadActual) FROM Inventario.InventarioStock s
                            JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaID
                            WHERE s.ArticuloID = fl.ArticuloID AND b.CentroCostoID = f.CentroCostoID), 0) AS StockDisponible
            FROM Facturacion.FacturaLineas fl
            JOIN Facturacion.Facturas f ON f.FacturaID = fl.FacturaID
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = fl.ArticuloID
            WHERE fl.FacturaID = @FacturaId AND fl.ArticuloID IS NOT NULL
            UNION ALL
            SELECT ci.ArticuloID, a.Referencia AS SkuArticulo, a.Nombre AS NombreArticulo,
                   fl.Cantidad * ci.Cantidad AS CantidadFacturada,
                   ISNULL((SELECT SUM(s.CantidadActual) FROM Inventario.InventarioStock s
                            JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaID
                            WHERE s.ArticuloID = ci.ArticuloID AND b.CentroCostoID = f.CentroCostoID), 0) AS StockDisponible
            FROM Facturacion.FacturaLineas fl
            JOIN Facturacion.Facturas f ON f.FacturaID = fl.FacturaID
            JOIN Marketing.ComboItems ci ON ci.ComboID = fl.ComboID
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = ci.ArticuloID
            WHERE fl.FacturaID = @FacturaId AND fl.ComboID IS NOT NULL
            ORDER BY SkuArticulo";

        var lineas = (await connection.QueryAsync<FacturaLineaStockItem>(sql, new { FacturaId = facturaId })).ToList();

        var ventaDesdeReceta = await connection.ExecuteScalarAsync<bool>(
            "SELECT ISNULL(VentaDesdeReceta, 0) FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");

        if (ventaDesdeReceta && lineas.Any(l => l.EsInsuficiente))
        {
            var ccId = await connection.ExecuteScalarAsync<int>(
                "SELECT CentroCostoID FROM Facturacion.Facturas WHERE FacturaID = @FacturaId",
                new { FacturaId = facturaId });

            // Stock efectivo desde receta: MIN(stock_insumo * rendimiento / cantidad_requerida) para todos los insumos.
            // Equivale a cuantas unidades del PT se pueden producir con el stock actual de insumos.
            const string sqlReceta = @"
                SELECT MIN(FLOOR(
                    ISNULL((SELECT SUM(s.CantidadActual)
                            FROM Inventario.InventarioStock s
                            JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaID
                            WHERE s.ArticuloID = rd.InsumoID AND b.CentroCostoID = @CcId), 0)
                    * r.CantidadRendimientoBase / NULLIF(rd.CantidadRequerida * (1 + rd.PorcentajeMermaEstandar / 100.0), 0)
                ))
                FROM Produccion.RecetaBOM r
                JOIN Produccion.RecetaBOM_Detalle rd ON rd.RecetaID = r.RecetaID
                WHERE r.ProductoTerminadoID = @ArticuloID AND r.Estado = 1
                  AND r.RecetaID = (
                      SELECT TOP 1 RecetaID FROM Produccion.RecetaBOM
                      WHERE ProductoTerminadoID = @ArticuloID AND Estado = 1
                      ORDER BY RecetaID)";

            for (var i = 0; i < lineas.Count; i++)
            {
                if (!lineas[i].EsInsuficiente) continue;

                var stockReceta = await connection.ExecuteScalarAsync<decimal?>(
                    sqlReceta, new { CcId = ccId, lineas[i].ArticuloID });

                if (stockReceta.HasValue && stockReceta.Value > lineas[i].StockDisponible)
                    lineas[i] = lineas[i] with { StockDisponible = stockReceta.Value };
            }
        }

        return lineas;
    }

    public async Task<int> ContarStockFallidoAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Facturacion.Facturas WHERE VisionsConfirmado = 1 AND StockDescontado = 0");
    }

    public async Task DescontarStockAsync(int facturaId, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        // Gate Visions: solo si el CC de esta factura tiene Visions integrado
        // se exige confirmación previa. CCs sin Visions descargan directo.
        var info = await connection.QuerySingleOrDefaultAsync<(bool? VisionsConfirmado, bool CcTieneVisions)>(
            @"SELECT f.VisionsConfirmado, ISNULL(cc.TieneVisions, 0) AS CcTieneVisions
              FROM Facturacion.Facturas f
              LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = f.CentroCostoID
              WHERE f.FacturaID = @FacturaID",
            new { FacturaID = facturaId });

        if (info.Equals(default))
            throw new KeyNotFoundException("Factura no encontrada.");

        if (info.CcTieneVisions && !info.VisionsConfirmado.GetValueOrDefault())
            throw new InvalidOperationException(
                "El centro de costo usa Visions. El descuento de inventario se aplica automáticamente cuando Visions confirme el documento.");

        var ventaDesdeReceta = await connection.ExecuteScalarAsync<bool>(
            "SELECT ISNULL(VentaDesdeReceta, 0) FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");

        var venderSinStock = await connection.ExecuteScalarAsync<bool>(
            "SELECT ISNULL(VenderSinStock, 0) FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");

        var spName = ventaDesdeReceta
            ? "Facturacion.sp_DescontarStockFactura_Receta"
            : "Facturacion.sp_DescontarStockFactura";

        try
        {
            var p = new DynamicParameters();
            p.Add("FacturaID", facturaId);
            p.Add("UsuarioID", usuarioId);
            p.Add("IgnorarStock", venderSinStock);
            await connection.ExecuteAsync(spName, p, commandType: System.Data.CommandType.StoredProcedure);
        }
        catch (Exception ex) when (ex.Message.Contains("ya fue descontado", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El stock de esta factura ya fue descontado anteriormente.");
        }
        catch (Exception ex) when (ex.Message.Contains("Stock insuficiente", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex) when (ex.Message.Contains("no tiene receta activa", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex) when (ex.Message.Contains("no encontrada", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("no tiene lineas", StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException(ex.Message);
        }
    }

    public async Task EliminarFacturaAsync(int facturaId)
    {
        using var connection = _db.CreateConnection();

        var factura = await connection.QuerySingleOrDefaultAsync<(bool StockDescontado, bool VisionsConfirmado, bool ExportadaVisions)>(
            @"SELECT f.StockDescontado, f.VisionsConfirmado,
                     CASE WHEN EXISTS (SELECT 1 FROM Facturacion.FacturasExportadasVisions fev WHERE fev.FacturaID = f.FacturaID) THEN 1 ELSE 0 END AS ExportadaVisions
              FROM Facturacion.Facturas f WHERE f.FacturaID = @FacturaID",
            new { FacturaID = facturaId });

        if (factura.Equals(default))
            throw new KeyNotFoundException("Factura no encontrada.");
        if (factura.StockDescontado)
            throw new InvalidOperationException("No se puede eliminar: el stock ya fue descontado.");
        if (factura.VisionsConfirmado)
            throw new InvalidOperationException("No se puede eliminar: la factura ya fue confirmada en Visions.");

        var saldoPendiente = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(f.Total - ISNULL((SELECT SUM(p.Monto) FROM Facturacion.Pagos p WHERE p.FacturaID = f.FacturaID), 0), 0) FROM Facturacion.Facturas f WHERE f.FacturaID = @FacturaID AND f.Total > 0",
            new { FacturaID = facturaId });
        if (saldoPendiente > 0)
            throw new InvalidOperationException($"No se puede eliminar: la factura tiene saldo pendiente de {saldoPendiente:C2}.");

        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync("DELETE FROM Facturacion.Pagos WHERE FacturaID = @Id", new { Id = facturaId }, tx);
            await connection.ExecuteAsync("DELETE FROM Facturacion.FacturasExportadasVisions WHERE FacturaID = @Id", new { Id = facturaId }, tx);
            await connection.ExecuteAsync("DELETE FROM Facturacion.FacturaLineas WHERE FacturaID = @Id", new { Id = facturaId }, tx);
            await connection.ExecuteAsync("DELETE FROM Facturacion.Facturas WHERE FacturaID = @Id", new { Id = facturaId }, tx);

            // Si ya se exportó al staging de Visions, encolar limpieza para que el agente la elimine allá también.
            if (factura.ExportadaVisions)
                await connection.ExecuteAsync(
                    "INSERT INTO Integracion.PendientesLimpiezaVisions (Tipo, EntidadID) VALUES ('FACTURA', @Id)",
                    new { Id = facturaId }, tx);

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task ConfirmarVisionsAsync(int facturaId)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Facturacion.Facturas SET VisionsConfirmado = 1 WHERE FacturaID = @FacturaID AND VisionsConfirmado = 0",
            new { FacturaID = facturaId });

        if (filas == 0)
        {
            var existe = await connection.ExecuteScalarAsync<bool?>(
                "SELECT 1 FROM Facturacion.Facturas WHERE FacturaID = @FacturaID",
                new { FacturaID = facturaId });
            if (existe is null)
                throw new KeyNotFoundException("Factura no encontrada.");
            // Si existe pero filas==0, ya estaba confirmada — no es error.
        }
    }

    public async Task<IEnumerable<PagoItem>> ListarPagosAsync(int facturaId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT p.PagoID, p.FacturaID, p.Monto, p.FechaPago, p.MetodoPago, p.Notas,
                   CONCAT(u.Nombres, ' ', u.Apellidos) AS Usuario
            FROM Facturacion.Pagos p
            LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = p.UsuarioID
            WHERE p.FacturaID = @FacturaId
            ORDER BY p.FechaPago DESC, p.PagoID DESC";

        return await connection.QueryAsync<PagoItem>(sql, new { FacturaId = facturaId });
    }

    public async Task<int> CrearPagoAsync(CrearPagoRequest r, int usuarioId)
    {
        if (r.Monto <= 0)
            throw new InvalidOperationException("El monto del pago debe ser mayor a cero.");

        using var connection = _db.CreateConnection();

        var saldo = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(f.Total - ISNULL((SELECT SUM(p.Monto) FROM Facturacion.Pagos p WHERE p.FacturaID = f.FacturaID), 0), 0) FROM Facturacion.Facturas f WHERE f.FacturaID = @FacturaID",
            new { r.FacturaID });

        if (r.Monto > saldo)
            throw new InvalidOperationException($"El monto ({r.Monto:C2}) supera el saldo pendiente ({saldo:C2}).");

        const string sql = @"
            INSERT INTO Facturacion.Pagos (FacturaID, Monto, FechaPago, MetodoPago, Notas, UsuarioID)
            OUTPUT INSERTED.PagoID
            VALUES (@FacturaID, @Monto, @FechaPago, @MetodoPago, @Notas, @UsuarioID)";

        return await connection.ExecuteScalarAsync<int>(sql,
            new { r.FacturaID, r.Monto, r.FechaPago, r.MetodoPago, r.Notas, UsuarioID = usuarioId });
    }

    // ──────────── Verificacion / auto-produccion ────────────
    private record LineaPTCruda(int ArticuloID, string SKU, string Nombre, decimal Cantidad, int? RecetaID);
    private record InsumoCrudo(int RecetaID, int ArticuloID, string InsumoNombre, string Unidad, decimal CantidadRequerida);
    private record StockCrudo(int ArticuloID, decimal StockTotal);

    public async Task<List<VerificarProduccionItem>> VerificarProduccionFacturaAsync(int facturaId)
    {
        using var conn = _db.CreateConnection();

        var ccId = await conn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(CentroCostoID, 0) FROM Facturacion.Facturas WHERE FacturaID = @facturaId",
            new { facturaId });

        var lineasPT = await conn.QueryAsync<LineaPTCruda>("""
            SELECT fl.ArticuloID, a.Referencia AS SKU, a.Nombre, fl.Cantidad,
                   (SELECT TOP 1 r.RecetaID FROM Produccion.RecetaBOM r
                    WHERE r.ProductoTerminadoID = fl.ArticuloID AND r.Estado = 1
                    ORDER BY r.RecetaID) AS RecetaID
            FROM Facturacion.FacturaLineas fl
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = fl.ArticuloID
            WHERE fl.FacturaID = @facturaId AND a.TipoArticuloID = 2
            UNION ALL
            SELECT ci.ArticuloID, a.Referencia AS SKU, a.Nombre, fl.Cantidad * ci.Cantidad AS Cantidad,
                   (SELECT TOP 1 r.RecetaID FROM Produccion.RecetaBOM r
                    WHERE r.ProductoTerminadoID = ci.ArticuloID AND r.Estado = 1) AS RecetaID
            FROM Facturacion.FacturaLineas fl
            JOIN Marketing.ComboItems ci ON ci.ComboID = fl.ComboID
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = ci.ArticuloID
            WHERE fl.FacturaID = @facturaId AND fl.ComboID IS NOT NULL AND a.TipoArticuloID = 2
            """, new { facturaId });

        var resultado = new List<VerificarProduccionItem>();

        foreach (var linea in lineasPT)
        {
            if (linea.RecetaID is null)
            {
                resultado.Add(new VerificarProduccionItem(
                    linea.ArticuloID, linea.SKU, linea.Nombre, linea.Cantidad, false, null, []));
                continue;
            }

            var insumos = await conn.QueryAsync<InsumoCrudo>("""
                SELECT rd.RecetaID, rd.InsumoID AS ArticuloID, a.Nombre AS InsumoNombre,
                       ISNULL(u.Abreviatura, '') AS Unidad,
                       rd.CantidadRequerida * @cantidad AS CantidadRequerida
                FROM Produccion.RecetaBOM_Detalle rd
                JOIN Catalogo.Tarjetas a ON a.ArticuloID = rd.InsumoID
                LEFT JOIN catalogo.UnidadesMedida u ON u.UnidadID = rd.UnidadID
                WHERE rd.RecetaID = @recetaId
                """, new { recetaId = linea.RecetaID, cantidad = linea.Cantidad });

            var insumoIds = insumos.Select(i => i.ArticuloID).Distinct().ToList();
            var stocks = insumoIds.Count == 0 ? []
                : (await conn.QueryAsync<StockCrudo>("""
                    SELECT s.ArticuloID, ISNULL(SUM(s.CantidadActual), 0) AS StockTotal
                    FROM Inventario.InventarioStock s
                    JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaID
                    WHERE s.ArticuloID IN @ids AND (@ccId = 0 OR b.CentroCostoID = @ccId)
                    GROUP BY s.ArticuloID
                    """, new { ids = insumoIds, ccId })).ToList();

            var stockDict = stocks.ToDictionary(s => s.ArticuloID, s => s.StockTotal);

            var listaInsumos = insumos.Select(i => new InsumoVerificacionItem(
                i.ArticuloID, i.InsumoNombre, i.Unidad,
                i.CantidadRequerida,
                stockDict.GetValueOrDefault(i.ArticuloID, 0)
            )).ToList();

            resultado.Add(new VerificarProduccionItem(
                linea.ArticuloID, linea.SKU, linea.Nombre,
                linea.Cantidad, true, linea.RecetaID, listaInsumos));
        }

        return resultado;
    }

    public async Task<List<AutoProducirResultItem>> AutoProducirFacturaAsync(int facturaId, int usuarioId)
    {
        var items = await VerificarProduccionFacturaAsync(facturaId);
        var resultado = new List<AutoProducirResultItem>();

        using var conn = _db.CreateConnection();
        var defaultTipoProduccion = await conn.ExecuteScalarAsync<int>(
            "SELECT TOP 1 TipoProduccionID FROM Produccion.TiposProduccion ORDER BY TipoProduccionID");
        var defaultCentroCosto = await conn.ExecuteScalarAsync<int>(
            "SELECT TOP 1 CentroCostoID FROM Organizacion.CentrosCosto WHERE Estado = 1");
        var defaultBodega = await conn.ExecuteScalarAsync<int>(
            "SELECT TOP 1 BodegaID FROM Inventario.Bodegas WHERE Estado = 1");

        foreach (var item in items.Where(i => i.TieneReceta))
        {
            try
            {
                var codigoOP = $"AP-{facturaId}-{item.ArticuloID}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var opId = await _produccion.CrearAsync(new CrearOrdenProduccionRequest(
                    CodigoOP: codigoOP,
                    TipoProduccionID: defaultTipoProduccion,
                    ProductoTerminadoID: item.ArticuloID,
                    RecetaID: item.RecetaID!.Value,
                    CantidadProgramada: item.CantidadFacturada,
                    ClienteID: null,
                    CentroCostoDestinoID: defaultCentroCosto,
                    BodegaOrigenMPID: defaultBodega,
                    BodegaDestinoPTID: defaultBodega,
                    CentroTrabajoID: null,
                    FechaPlanificada: NexoApi.Common.ColombiaClock.Today,
                    Observaciones: $"Auto-generada desde factura #{facturaId}"
                ), usuarioId);

                await _produccion.LiberarAsync(opId, usuarioId);
                await _produccion.IniciarAsync(opId, usuarioId);
                await _produccion.CerrarAsync(opId, new CerrarOrdenProduccionRequest(
                    CantidadProducidaReal: item.CantidadFacturada,
                    HorasManoObra: 0, HorasCIF: 0,
                    NumeroLotePT: $"AUTO-{codigoOP}",
                    FechaVencimientoPT: null
                ), usuarioId);

                resultado.Add(new AutoProducirResultItem(item.ArticuloID, item.Nombre, true, opId, null));
            }
            catch (Exception ex)
            {
                resultado.Add(new AutoProducirResultItem(item.ArticuloID, item.Nombre, false, null, ex.Message));
            }
        }

        if (resultado.Any(r => r.Exitoso))
        {
            using var conn2 = _db.CreateConnection();

            // AutoProducir siempre usa sp_DescontarStockFactura (sin receta):
            // el ciclo OP ya dedujo los insumos y añadió el PT al stock;
            // aquí solo hay que descontar el PT de la factura.
            // Llamar a DescontarStockAsync aquí provocaría doble descuento de
            // insumos cuando VentaDesdeReceta = ON.
            var ignorarStockAuto = await conn2.ExecuteScalarAsync<bool>(
                "SELECT ISNULL(VenderSinStock, 0) FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");
            var pAuto = new Dapper.DynamicParameters();
            pAuto.Add("FacturaID", facturaId);
            pAuto.Add("UsuarioID", usuarioId);
            pAuto.Add("IgnorarStock", ignorarStockAuto);
            await conn2.ExecuteAsync(
                "Facturacion.sp_DescontarStockFactura", pAuto,
                commandType: System.Data.CommandType.StoredProcedure);

            await conn2.ExecuteAsync(
                "UPDATE Facturacion.Facturas SET ProduccionAutoEjecutada=1 WHERE FacturaID=@facturaId",
                new { facturaId });
        }

        return resultado;
    }

    public async Task<UltimoPrecioItem?> ObtenerUltimoPrecioAsync(int clienteId, int articuloId)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT TOP 1
                fl.PrecioUnitario AS Precio,
                f.Fecha,
                f.NroDoc
            FROM Facturacion.FacturaLineas fl
            JOIN Facturacion.Facturas f ON f.FacturaID = fl.FacturaID
            WHERE f.ClienteID = @ClienteId
              AND fl.ArticuloID = @ArticuloId
              AND fl.PrecioUnitario > 0
              AND f.TipDoc IN ('FACTURA','REMISION')
            ORDER BY f.Fecha DESC, f.FacturaID DESC";
        return await connection.QuerySingleOrDefaultAsync<UltimoPrecioItem>(
            sql, new { ClienteId = clienteId, ArticuloId = articuloId });
    }
}
