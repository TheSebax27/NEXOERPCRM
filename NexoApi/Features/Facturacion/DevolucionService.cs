using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Facturacion.Dtos;

namespace NexoApi.Features.Facturacion;

public interface IDevolucionService
{
    Task<IEnumerable<DevolucionItem>> ListarAsync(string? tipo, int? clienteId, int? proveedorId, DateTime? desde, DateTime? hasta);
    Task<int> CrearAsync(CrearDevolucionRequest request, int usuarioId);
    Task<IEnumerable<DevolucionLineaItem>> ListarLineasAsync(int devolucionId);
    Task<DevolucionesAnalytics> ObtenerAnalyticsAsync(int meses);
}

public class DevolucionService(IDbConnectionFactory db) : IDevolucionService
{
    public async Task<IEnumerable<DevolucionItem>> ListarAsync(
        string? tipo, int? clienteId, int? proveedorId, DateTime? desde, DateTime? hasta)
    {
        using var conn = db.CreateConnection();
        var desdeEfectivo = desde ?? DateTime.UtcNow.AddMonths(-6);
        return await conn.QueryAsync<DevolucionItem>(@"
            SELECT TOP 500
                   d.DevolucionID, d.TipoDevolucion,
                   d.FacturaOrigenID,
                   f.NroDoc AS NroDocOrigen,
                   d.ClienteID,  c.Nombre  AS Cliente,
                   d.ProveedorID, p.RazonSocial AS Proveedor,
                   d.CentroCostoID, cc.Nombre AS CentroCosto,
                   d.Fecha, d.Motivo, d.NroDoc,
                   d.Total, d.StockRestituido, d.OrigenVisions,
                   u.Nombre AS Usuario, d.FechaRegistro
            FROM Facturacion.Devoluciones d
            LEFT JOIN Facturacion.Facturas   f  ON f.FacturaID   = d.FacturaOrigenID
            LEFT JOIN Crm.Clientes           c  ON c.ClienteID   = d.ClienteID
            LEFT JOIN Catalogo.Proveedores   p  ON p.ProveedorID = d.ProveedorID
            LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = d.CentroCostoID
            LEFT JOIN Seguridad.Usuarios     u  ON u.UsuarioID   = d.UsuarioID
            WHERE (@tipo       IS NULL OR d.TipoDevolucion = @tipo)
              AND (@clienteId  IS NULL OR d.ClienteID      = @clienteId)
              AND (@proveedorId IS NULL OR d.ProveedorID   = @proveedorId)
              AND d.Fecha >= @desdeEfectivo
              AND (@hasta      IS NULL OR d.Fecha         <= @hasta)
            ORDER BY d.Fecha DESC, d.DevolucionID DESC",
            new { tipo, clienteId, proveedorId, desdeEfectivo, hasta });
    }

    public async Task<int> CrearAsync(CrearDevolucionRequest r, int usuarioId)
    {
        using var conn = db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var total = r.Lineas.Sum(l => l.Cantidad * l.CostoUnitario);

        var devId = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Facturacion.Devoluciones
                (TipoDevolucion, FacturaOrigenID, ClienteID, ProveedorID, CentroCostoID,
                 Fecha, Motivo, NroDoc, Total, StockRestituido, OrigenVisions, UsuarioID)
            OUTPUT INSERTED.DevolucionID
            VALUES (@TipoDevolucion, @FacturaOrigenID, @ClienteID, @ProveedorID, @CentroCostoID,
                    @Fecha, @Motivo, @NroDoc, @Total, 0, 0, @UsuarioID)",
            new { r.TipoDevolucion, r.FacturaOrigenID, r.ClienteID, r.ProveedorID, r.CentroCostoID,
                  r.Fecha, r.Motivo, r.NroDoc, Total = total, UsuarioID = usuarioId }, tx);

        // Lookups fijos para toda la devolución — se resuelven una sola vez antes del loop
        int tipoMovCliente = 0, tipoMovProveedor = 0;
        int? bodegaIdFijo = null;

        if (r.TipoDevolucion == "CLIENTE")
        {
            tipoMovCliente = await conn.ExecuteScalarAsync<int>(
                "SELECT TipoMovID FROM Kardex.TiposMovimientoKardex WHERE Codigo = 'ENTRADA_DEVOLUCION_CLIENTE'",
                transaction: tx);
            if (tipoMovCliente == 0)
                throw new InvalidOperationException("Tipo de movimiento Kardex 'ENTRADA_DEVOLUCION_CLIENTE' no encontrado. Verifique la configuración.");

            if (r.CentroCostoID.HasValue)
                bodegaIdFijo = await conn.ExecuteScalarAsync<int?>(
                    "SELECT BodegaVentaVisionsID FROM Organizacion.CentrosCosto WHERE CentroCostoID = @Id",
                    new { Id = r.CentroCostoID.Value }, tx);

            if (!bodegaIdFijo.HasValue)
                throw new InvalidOperationException(
                    "El Centro de Costo no tiene Bodega de Venta configurada. Seleccione un Centro de Costo válido.");
        }
        else if (r.TipoDevolucion == "PROVEEDOR")
        {
            tipoMovProveedor = await conn.ExecuteScalarAsync<int>(
                "SELECT TipoMovID FROM Kardex.TiposMovimientoKardex WHERE Codigo = 'SALIDA_DEVOLUCION_PROVEEDOR'",
                transaction: tx);
            if (tipoMovProveedor == 0)
                throw new InvalidOperationException("Tipo de movimiento Kardex 'SALIDA_DEVOLUCION_PROVEEDOR' no encontrado. Verifique la configuración.");

            if (r.CentroCostoID.HasValue)
                bodegaIdFijo = await conn.ExecuteScalarAsync<int?>(
                    "SELECT BodegaVentaVisionsID FROM Organizacion.CentrosCosto WHERE CentroCostoID = @Id",
                    new { Id = r.CentroCostoID.Value }, tx);

            if (!bodegaIdFijo.HasValue)
                throw new InvalidOperationException(
                    "El Centro de Costo no tiene Bodega de Venta configurada. Seleccione un Centro de Costo válido.");
        }

        foreach (var linea in r.Lineas)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO Facturacion.DevolucionLineas (DevolucionID, ArticuloID, Cantidad, CostoUnitario, Nota)
                VALUES (@DevolucionID, @ArticuloID, @Cantidad, @CostoUnitario, @Nota)",
                new { DevolucionID = devId, linea.ArticuloID, linea.Cantidad, linea.CostoUnitario, linea.Nota }, tx);

            // Restituir o retirar stock según el tipo
            if (r.TipoDevolucion == "CLIENTE")
            {
                var tipoMov  = tipoMovCliente;
                var bodegaId = bodegaIdFijo;

                if (bodegaId.HasValue)
                {
                    await conn.ExecuteAsync(@"
                        MERGE Inventario.InventarioStock AS dest
                        USING (SELECT @ArtID AS ArticuloID, @BodID AS BodegaID) AS src
                        ON dest.ArticuloID = src.ArticuloID AND dest.BodegaID = src.BodegaID AND dest.LoteID IS NULL
                        WHEN MATCHED THEN
                            UPDATE SET CantidadActual = CantidadActual + @Cantidad, FechaUltimaActualizacion = SYSUTCDATETIME()
                        WHEN NOT MATCHED THEN
                            INSERT (ArticuloID, BodegaID, LoteID, CantidadActual, CostoUnitarioLote, FechaUltimaActualizacion)
                            VALUES (@ArtID, @BodID, NULL, @Cantidad, @Costo, SYSUTCDATETIME());",
                        new { ArtID = linea.ArticuloID, BodID = bodegaId.Value, linea.Cantidad, Costo = linea.CostoUnitario }, tx);

                    var saldo = await conn.ExecuteScalarAsync<decimal>(
                        "SELECT ISNULL(SUM(CantidadActual),0) FROM Inventario.InventarioStock WHERE ArticuloID=@ArtID AND BodegaID=@BodID",
                        new { ArtID = linea.ArticuloID, BodID = bodegaId.Value }, tx);

                    await conn.ExecuteAsync(@"
                        INSERT INTO Kardex.KardexMovimientos
                            (ArticuloID, BodegaID, LoteID, TipoMovID, CentroCostoID, Cantidad, CostoUnitario,
                             CantidadSaldo, CostoPromedioSaldo, ObservacionDetallada, UsuarioID)
                        VALUES (@ArtID, @BodID, NULL, @TipoMov, @CC, @Cantidad, @Costo,
                                @Saldo, @Costo, @Obs, @UserID)",
                        new { ArtID = linea.ArticuloID, BodID = bodegaId.Value, TipoMov = tipoMov,
                              CC = r.CentroCostoID, linea.Cantidad, Costo = linea.CostoUnitario,
                              Saldo = saldo, Obs = $"Devolucion cliente #{devId}", UserID = usuarioId }, tx);
                }
            }
            else if (r.TipoDevolucion == "PROVEEDOR")
            {
                var tipoMov  = tipoMovProveedor;
                var bodegaId = bodegaIdFijo;

                if (bodegaId.HasValue)
                {
                    var stockDisponible = await conn.ExecuteScalarAsync<decimal>(
                        "SELECT ISNULL(SUM(CantidadActual),0) FROM Inventario.InventarioStock WHERE ArticuloID=@ArtID AND BodegaID=@BodID AND LoteID IS NULL",
                        new { ArtID = linea.ArticuloID, BodID = bodegaId.Value }, tx);

                    if (stockDisponible < linea.Cantidad)
                        throw new InvalidOperationException(
                            $"Stock insuficiente para el artículo ID {linea.ArticuloID}: disponible {stockDisponible}, solicitado {linea.Cantidad}.");

                    await conn.ExecuteAsync(@"
                        UPDATE Inventario.InventarioStock
                        SET CantidadActual = CantidadActual - @Cantidad, FechaUltimaActualizacion = SYSUTCDATETIME()
                        WHERE ArticuloID = @ArtID AND BodegaID = @BodID AND LoteID IS NULL",
                        new { ArtID = linea.ArticuloID, BodID = bodegaId.Value, linea.Cantidad }, tx);

                    var saldo = await conn.ExecuteScalarAsync<decimal>(
                        "SELECT ISNULL(SUM(CantidadActual),0) FROM Inventario.InventarioStock WHERE ArticuloID=@ArtID AND BodegaID=@BodID",
                        new { ArtID = linea.ArticuloID, BodID = bodegaId.Value }, tx);

                    await conn.ExecuteAsync(@"
                        INSERT INTO Kardex.KardexMovimientos
                            (ArticuloID, BodegaID, LoteID, TipoMovID, CentroCostoID, Cantidad, CostoUnitario,
                             CantidadSaldo, CostoPromedioSaldo, ObservacionDetallada, UsuarioID)
                        VALUES (@ArtID, @BodID, NULL, @TipoMov, @CC, @Cantidad, @Costo,
                                @Saldo, @Costo, @Obs, @UserID)",
                        new { ArtID = linea.ArticuloID, BodID = bodegaId.Value, TipoMov = tipoMov,
                              CC = r.CentroCostoID, linea.Cantidad, Costo = linea.CostoUnitario,
                              Saldo = saldo, Obs = $"Devolucion proveedor #{devId}", UserID = usuarioId }, tx);
                }
            }
        }

        await conn.ExecuteAsync(
            "UPDATE Facturacion.Devoluciones SET StockRestituido=1 WHERE DevolucionID=@Id",
            new { Id = devId }, tx);

        tx.Commit();
        return devId;
    }

    public async Task<IEnumerable<DevolucionLineaItem>> ListarLineasAsync(int devolucionId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<DevolucionLineaItem>(@"
            SELECT dl.LineaID, dl.DevolucionID,
                   dl.ArticuloID, a.Referencia AS SKU, a.Nombre AS Articulo,
                   dl.Cantidad, dl.CostoUnitario, dl.Subtotal, dl.Nota
            FROM Facturacion.DevolucionLineas dl
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = dl.ArticuloID
            WHERE dl.DevolucionID = @devolucionId
            ORDER BY dl.LineaID",
            new { devolucionId });
    }

    public async Task<DevolucionesAnalytics> ObtenerAnalyticsAsync(int meses)
    {
        using var conn = db.CreateConnection();
        var desde = NexoApi.Common.ColombiaClock.Today.AddMonths(-meses);

        var total = await conn.QueryFirstOrDefaultAsync<(int Cantidad, decimal Valor)>(@"
            SELECT COUNT(*) AS Cantidad, ISNULL(SUM(Total),0) AS Valor
            FROM Facturacion.Devoluciones
            WHERE Fecha >= @desde", new { desde });

        var totalVentas = await conn.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(fl.Cantidad * fl.PrecioUnitario), 0)
            FROM Facturacion.FacturaLineas fl
            JOIN Facturacion.Facturas f ON f.FacturaID = fl.FacturaID
            WHERE f.Fecha >= @desde", new { desde });

        var tasa = totalVentas > 0 ? (total.Valor / totalVentas * 100) : 0;

        var porMes = (await conn.QueryAsync<DevolucionesResumenMes>(@"
            SELECT FORMAT(Fecha,'yyyy-MM') AS Mes,
                   COUNT(*) AS Cantidad,
                   ISNULL(SUM(Total),0) AS ValorDevuelto
            FROM Facturacion.Devoluciones
            WHERE Fecha >= @desde
            GROUP BY FORMAT(Fecha,'yyyy-MM')
            ORDER BY Mes", new { desde })).ToList();

        var topArticulos = (await conn.QueryAsync<ArticuloMasDevuelto>(@"
            SELECT TOP 10
                dl.ArticuloID, a.Referencia AS SKU, a.Nombre AS Articulo,
                COUNT(DISTINCT dl.DevolucionID) AS VecesDevuelto,
                SUM(dl.Cantidad)               AS CantidadTotal,
                SUM(dl.Subtotal)               AS ValorTotal
            FROM Facturacion.DevolucionLineas dl
            JOIN Facturacion.Devoluciones d ON d.DevolucionID = dl.DevolucionID
            JOIN Catalogo.Tarjetas a        ON a.ArticuloID   = dl.ArticuloID
            WHERE d.Fecha >= @desde
            GROUP BY dl.ArticuloID, a.Referencia, a.Nombre
            ORDER BY ValorTotal DESC", new { desde })).ToList();

        return new DevolucionesAnalytics(total.Cantidad, total.Valor, tasa, porMes, topArticulos);
    }
}
