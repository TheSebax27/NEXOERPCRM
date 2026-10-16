using Dapper;
using Microsoft.Extensions.Caching.Memory;
using NexoApi.Common.Data;
using NexoApi.Features.Catalogo.Dtos;


namespace NexoApi.Features.Catalogo;

public interface ICatalogoService
{
    // Centros de Costo
    Task<int> CrearCentroCostoAsync(CrearCentroCostoRequest request);
    Task<IEnumerable<CentroCostoItem>> ListarCentrosCostoAsync(bool soloActivos);
    Task ActualizarCentroCostoAsync(int centroCostoId, ActualizarCentroCostoRequest request);

    // Bodegas
    Task<int> CrearBodegaAsync(CrearBodegaRequest request);
    Task<IEnumerable<BodegaItem>> ListarBodegasAsync(int? centroCostoId);
    Task ActualizarBodegaAsync(int bodegaId, ActualizarBodegaRequest request);
    Task DesactivarBodegaAsync(int bodegaId);

    // Articulos / Tarjetas
    Task<int> CrearArticuloAsync(CrearArticuloRequest request);
    Task<ArticulosPaginadosResponse> ListarArticulosAsync(int? tipoArticuloId, string? texto, bool? estado = null, int pagina = 1, int tamano = 100, int? centroCostoId = null);
    Task<IEnumerable<ArticuloItem>> ListarTodosArticulosAsync(int? tipoArticuloId = null, bool? estado = null);
    Task<int> ContarArticulosAsync(bool? estado = null);
    Task<IEnumerable<ArticuloItem>> BuscarArticulosAsync(string? q, bool? soloTerminados = null, int max = 30);
    Task<ArticuloItem?> ObtenerArticuloAsync(int articuloId);
    Task ActualizarArticuloAsync(int articuloId, ActualizarArticuloRequest request, int usuarioId, string nombreUsuario);
    Task<IEnumerable<HistorialPrecioItem>> ListarHistorialPreciosAsync(int articuloId);
    Task<(byte[] Datos, string ContentType)?> ObtenerImagenArticuloAsync(int articuloId);
    Task ActualizarImagenArticuloAsync(int articuloId, ActualizarImagenRequest request);
    Task EliminarImagenArticuloAsync(int articuloId);

    Task<IEnumerable<CentroTrabajoItem>> ListarCentrosTrabajoAsync(bool soloActivos);
    Task<int> CrearCentroTrabajoAsync(CrearCentroTrabajoRequest request);
    Task ActualizarCentroTrabajoAsync(int centroTrabajoId, ActualizarCentroTrabajoRequest request);

    Task<IEnumerable<ProveedorItem>> ListarProveedoresAsync();
    Task<ProveedorItem?> ObtenerProveedorAsync(int proveedorId);
    Task<int> CrearProveedorAsync(CrearProveedorRequest request);
    Task ActualizarProveedorAsync(int proveedorId, ActualizarProveedorRequest request);
    Task<DualRolInfo> VerificarDualRolAsync(string nit);
    Task<int> AgregarComoProveedorDesdeClienteAsync(int clienteId);
    Task<int> AgregarComoClienteDesdeProveedorAsync(int proveedorId);
    Task<IEnumerable<NexoApi.Features.Crm.Dtos.EventoHistorialProveedorItem>> ObtenerHistorialProveedorAsync(int proveedorId);

    Task<IEnumerable<TipoArticuloItem>> ListarTiposArticuloAsync();
    Task<IEnumerable<UnidadMedidaItem>> ListarUnidadesMedidaAsync();

    // Variantes
    Task<IEnumerable<VarianteItem>> ListarVariantesAsync(int padreId);
    Task<int> CrearVarianteAsync(int padreId, CrearVarianteRequest request);

    // Catalogo: Iva (calca de dbo.IVA de Visions)
    Task<IEnumerable<IvaItem>> ListarIvasAsync();
    Task<int> CrearIvaAsync(CrearIvaRequest request);
    Task ActualizarIvaAsync(int ivaId, ActualizarIvaRequest request);
    Task EliminarIvaAsync(int ivaId);

    // Catalogos Visions: GruposMayores, GruposMenores, Marcas, Presentaciones
    Task<IEnumerable<GrupoMayorItem>> ListarGruposMayoresAsync();
    Task CrearGrupoMayorAsync(CrearGrupoMayorRequest request);
    Task ActualizarGrupoMayorAsync(string codigo, ActualizarGrupoMayorRequest request);
    Task EliminarGrupoMayorAsync(string codigo);

    Task<IEnumerable<GrupoMenorItem>> ListarGruposMenoresAsync(string? grupoMayor);
    Task CrearGrupoMenorAsync(CrearGrupoMenorRequest request);
    Task ActualizarGrupoMenorAsync(string codigo, string grupoMayor, ActualizarGrupoMenorRequest request);
    Task EliminarGrupoMenorAsync(string codigo, string grupoMayor);

    Task<IEnumerable<MarcaItem>> ListarMarcasAsync();
    Task CrearMarcaAsync(CrearMarcaRequest request);
    Task ActualizarMarcaAsync(string codigo, ActualizarMarcaRequest request);
    Task EliminarMarcaAsync(string codigo);

    Task<IEnumerable<PresentacionItem>> ListarPresentacionesAsync();
    Task CrearPresentacionAsync(CrearPresentacionRequest request);
    Task ActualizarPresentacionAsync(string codigo, ActualizarPresentacionRequest request);
    Task EliminarPresentacionAsync(string codigo);
}

public class CatalogoService : ICatalogoService
{
    private readonly IDbConnectionFactory _db;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan _ttlCatalogos = TimeSpan.FromMinutes(5);

    public CatalogoService(IDbConnectionFactory db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    // ================= Centros de Costo =================

    public async Task<int> CrearCentroCostoAsync(CrearCentroCostoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Organizacion.CentrosCosto (Codigo, Nombre, TipoCentro, Direccion, Telefono)
            OUTPUT INSERTED.CentroCostoID
            VALUES (@Codigo, @Nombre, @TipoCentro, @Direccion, @Telefono)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task<IEnumerable<CentroCostoItem>> ListarCentrosCostoAsync(bool soloActivos)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT CentroCostoID, Codigo, Nombre, TipoCentro, Direccion, Estado,
                   TieneVisions, IdentificadorClienteVisions, BodegaVentaVisionsID,
                   PrefijosDocumentoVentaVisions
            FROM Organizacion.CentrosCosto
            WHERE (@SoloActivos = 0 OR Estado = 1)
            ORDER BY Nombre";

        return await connection.QueryAsync<CentroCostoItem>(sql, new { SoloActivos = soloActivos });
    }

    public async Task ActualizarCentroCostoAsync(int centroCostoId, ActualizarCentroCostoRequest r)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var txn = connection.BeginTransaction();

        var tieneVisionsPrevio = await connection.ExecuteScalarAsync<bool>(
            "SELECT ISNULL(TieneVisions, 0) FROM Organizacion.CentrosCosto WHERE CentroCostoID = @Id",
            new { Id = centroCostoId }, txn);

        const string sql = @"
            UPDATE Organizacion.CentrosCosto
            SET Nombre = @Nombre, Direccion = @Direccion, Telefono = @Telefono, Estado = @Estado,
                TieneVisions = @TieneVisions, IdentificadorClienteVisions = @IdentificadorClienteVisions,
                BodegaVentaVisionsID = @BodegaVentaVisionsID,
                PrefijosDocumentoVentaVisions = @PrefijosDocumentoVentaVisions
            WHERE CentroCostoID = @CentroCostoId";

        var filas = await connection.ExecuteAsync(sql, new
        {
            CentroCostoId = centroCostoId,
            r.Nombre,
            r.Direccion,
            r.Telefono,
            r.Estado,
            r.TieneVisions,
            r.IdentificadorClienteVisions,
            r.BodegaVentaVisionsID,
            r.PrefijosDocumentoVentaVisions
        }, txn);

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el centro de costo {centroCostoId}.");

        // Backfill automático: CC recién activado para Visions → mapear y encolar todos los artículos activos.
        // Dentro de la misma transacción: si el backfill falla, el UPDATE también se revierte.
        if (r.TieneVisions && !tieneVisionsPrevio)
            await BackfillArticulosParaCcAsync(connection, txn, centroCostoId);

        txn.Commit();
    }

    private static async Task BackfillArticulosParaCcAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction txn, int centroCostoId)
    {
        await connection.ExecuteAsync(@"
            INSERT INTO Integracion.MapeoArticulos (ArticuloID, CentroCostoID, CodigoArticuloVisions, Estado)
            SELECT t.ArticuloID, @CcId, t.Referencia, 1
            FROM Catalogo.Tarjetas t
            WHERE t.Estado = 1
              AND NOT EXISTS (
                  SELECT 1 FROM Integracion.MapeoArticulos ma
                  WHERE ma.ArticuloID = t.ArticuloID AND ma.CentroCostoID = @CcId);

            INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
            SELECT 'SINCRONIZAR_ARTICULO', @CcId, ma.ArticuloID,
                   ISNULL(stk.Total, 0),
                   ISNULL(t.CostoPromedio, 0)
            FROM Integracion.MapeoArticulos ma
            JOIN Catalogo.Tarjetas t ON t.ArticuloID = ma.ArticuloID
            LEFT JOIN (
                SELECT s.ArticuloID, SUM(s.CantidadActual) AS Total
                FROM Inventario.InventarioStock s
                JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaID
                WHERE b.CentroCostoID = @CcId
                GROUP BY s.ArticuloID
            ) stk ON stk.ArticuloID = ma.ArticuloID
            WHERE ma.CentroCostoID = @CcId AND ma.Estado = 1
              AND NOT EXISTS (
                  SELECT 1 FROM Integracion.EventosSalientes e
                  WHERE e.ArticuloID = ma.ArticuloID AND e.CentroCostoID = @CcId
                    AND e.TipoEvento = 'SINCRONIZAR_ARTICULO' AND e.Estado IN ('PENDIENTE','CONFIRMADO'));",
            new { CcId = centroCostoId }, txn);
    }

    // ================= Bodegas =================

    public async Task<int> CrearBodegaAsync(CrearBodegaRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Inventario.Bodegas (Nombre, CentroCostoID, TipoBodega, EsVirtual)
            OUTPUT INSERTED.BodegaID
            VALUES (@Nombre, @CentroCostoID, @TipoBodega, @EsVirtual)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task<IEnumerable<BodegaItem>> ListarBodegasAsync(int? centroCostoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT b.BodegaID, b.Nombre, b.CentroCostoID, cc.Nombre AS CentroCosto,
                   b.TipoBodega, b.EsVirtual, b.Estado
            FROM Inventario.Bodegas b
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = b.CentroCostoID
            WHERE (@CentroCostoId IS NULL OR b.CentroCostoID = @CentroCostoId)
            ORDER BY cc.Nombre, b.Nombre";

        return await connection.QueryAsync<BodegaItem>(sql, new { CentroCostoId = centroCostoId });
    }

    public async Task ActualizarBodegaAsync(int bodegaId, ActualizarBodegaRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            UPDATE Inventario.Bodegas
            SET Nombre = @Nombre, TipoBodega = @TipoBodega, EsVirtual = @EsVirtual, Estado = @Estado
            WHERE BodegaID = @BodegaId";

        var filas = await connection.ExecuteAsync(sql, new
        {
            BodegaId = bodegaId,
            r.Nombre,
            r.TipoBodega,
            r.EsVirtual,
            r.Estado
        });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe la bodega {bodegaId}.");
    }

    public async Task DesactivarBodegaAsync(int bodegaId)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Inventario.Bodegas SET Estado = 0 WHERE BodegaID = @BodegaId",
            new { BodegaId = bodegaId });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe la bodega {bodegaId}.");
    }

    // ================= Articulos / Tarjetas =================

    public async Task<int> CrearArticuloAsync(CrearArticuloRequest r)
    {
        using var connection = _db.CreateConnection();

        // ICO no admite segundo impuesto
        if (r.Iva2.HasValue && r.Iva2 != 0 && r.IvaValor.HasValue)
        {
            var esICO = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Catalogo.Iva WHERE Iva = @Iva AND ISNULL(TipoImpuesto,'IVA') = 'ICO'",
                new { Iva = r.IvaValor.Value });
            if (esICO > 0)
                throw new InvalidOperationException("El ICO solo puede ser impuesto principal. Un artículo con ICO no puede tener segundo impuesto.");
        }

        // Auto-crear Marca y Presentacion si el codigo llega desde Visions pero aun no existe en NEXO.
        // GrupoMenor no se auto-crea porque requiere GrupoMayor (FK no-nullable).
        const string sqlEnsureCatalogos = @"
            IF @MarcaCodigo IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Catalogo.Marca WHERE Codigo = @MarcaCodigo)
                INSERT INTO Catalogo.Marca (Codigo, Marca) VALUES (@MarcaCodigo, @MarcaCodigo);
            IF @PresentacionCodigo IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Catalogo.Presentacion WHERE Codigo = @PresentacionCodigo)
                INSERT INTO Catalogo.Presentacion (Codigo, Presentacion) VALUES (@PresentacionCodigo, @PresentacionCodigo);";

        await connection.ExecuteAsync(sqlEnsureCatalogos, new { r.MarcaCodigo, r.PresentacionCodigo });

        const string sql = @"
            INSERT INTO Catalogo.Tarjetas
                (Referencia, Nombre, Descripcion, TipoArticuloID, StockMinimo, PuntoReorden,
                 DiasVidaUtil, Fracciona, PrecioVentaUnidad,
                 Costo, PPublico, PBodega, PCredito, UPublico, UBodega, UCredito,
                 MarcaCodigo, GrupoMenorCodigo, PresentacionCodigo,
                 Peso, IvaSiNo, IvaValor, IvaDescripcion, Iva2, IvaDescripcion2)
            OUTPUT INSERTED.ArticuloID
            VALUES
                (@Referencia, @Nombre, @Descripcion, @TipoArticuloID, @StockMinimo, @PuntoReorden,
                 @DiasVidaUtil, ISNULL(@Fracciona, 'SI'), @PrecioVentaUnidad,
                 @Costo, @PPublico, @PBodega, @PCredito, @UPublico, @UBodega, @UCredito,
                 @MarcaCodigo,
                 CASE WHEN EXISTS (SELECT 1 FROM Catalogo.GrupoMenor WHERE Codigo = @GrupoMenorCodigo) THEN @GrupoMenorCodigo ELSE NULL END,
                 @PresentacionCodigo,
                 @Peso, @IvaSiNo, @IvaValor, @IvaDescripcion, @Iva2, @IvaDescripcion2)";

        var articuloId = await connection.ExecuteScalarAsync<int>(sql, r);

        // Auto-mapear a todos los centros de costo con Visions y crear evento de sync inicial.
        // Usa la propia Referencia del articulo como CodigoArticuloVisions (convencion NEXO↔Visions).
        await connection.ExecuteAsync(@"
            INSERT INTO Integracion.MapeoArticulos (ArticuloID, CentroCostoID, CodigoArticuloVisions, Estado)
            SELECT @ArticuloId, cc.CentroCostoID, @Referencia, 1
            FROM Organizacion.CentrosCosto cc
            WHERE cc.TieneVisions = 1
              AND NOT EXISTS (
                  SELECT 1 FROM Integracion.MapeoArticulos ma
                  WHERE ma.ArticuloID = @ArticuloId AND ma.CentroCostoID = cc.CentroCostoID);

            INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
            SELECT 'SINCRONIZAR_ARTICULO', ma.CentroCostoID, ma.ArticuloID, 0, ISNULL(a.CostoPromedio, 0)
            FROM Integracion.MapeoArticulos ma
            JOIN Catalogo.Tarjetas a          ON a.ArticuloID     = ma.ArticuloID
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
            WHERE ma.ArticuloID = @ArticuloId AND ma.Estado = 1 AND cc.TieneVisions = 1",
            new { ArticuloId = articuloId, r.Referencia });

        return articuloId;
    }

    public async Task<ArticulosPaginadosResponse> ListarArticulosAsync(int? tipoArticuloId, string? texto, bool? estado = null, int pagina = 1, int tamano = 100, int? centroCostoId = null)
    {
        using var connection = _db.CreateConnection();
        var offset = (pagina - 1) * tamano;

        // Cuando se filtra por CC: solo artículos mapeados a ese CC, stock de la bodega de ese CC.
        // Cuando no: todos los artículos, stock total sumado de todas las bodegas.
        var stkJoin = centroCostoId.HasValue
            ? @"LEFT JOIN (
                SELECT ist.ArticuloID, SUM(ist.CantidadActual) AS Existencias
                FROM Inventario.InventarioStock ist
                JOIN Inventario.Bodegas b ON b.BodegaID = ist.BodegaID
                WHERE b.CentroCostoID = @CentroCostoId
                GROUP BY ist.ArticuloID
            ) stk ON stk.ArticuloID = a.ArticuloID"
            : @"LEFT JOIN (
                SELECT ArticuloID, SUM(CantidadActual) AS Existencias
                FROM Inventario.InventarioStock
                GROUP BY ArticuloID
            ) stk ON stk.ArticuloID = a.ArticuloID";

        var sqlBase = @"
            FROM Catalogo.Tarjetas a
            JOIN Catalogo.TiposArticulo ta ON ta.TipoArticuloID = a.TipoArticuloID
            LEFT JOIN Catalogo.Marca m ON m.Codigo = a.MarcaCodigo
            LEFT JOIN Catalogo.GrupoMenor gm ON gm.Codigo = a.GrupoMenorCodigo
            LEFT JOIN Catalogo.GrupoMayor gmay ON gmay.Codigo = gm.GrupoMayor
            LEFT JOIN Catalogo.Presentacion p ON p.Codigo = a.PresentacionCodigo
            " + stkJoin + @"
            WHERE (@TipoArticuloId IS NULL OR a.TipoArticuloID = @TipoArticuloId)
              AND (@Texto IS NULL OR a.Nombre LIKE '%' + @Texto + '%' OR a.Referencia LIKE '%' + @Texto + '%')
              AND (@Estado IS NULL OR a.Estado = @Estado)
              AND (@CentroCostoId IS NULL OR EXISTS (
                  SELECT 1 FROM Integracion.MapeoArticulos ma
                  WHERE ma.ArticuloID = a.ArticuloID AND ma.CentroCostoID = @CentroCostoId AND ma.Estado = 1))";

        var p = new { TipoArticuloId = tipoArticuloId, Texto = texto, Estado = estado, CentroCostoId = centroCostoId };

        var total = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) " + sqlBase, p);

        var items = (await connection.QueryAsync<ArticuloItem>(@"
            SELECT a.ArticuloID, a.Referencia, a.Nombre, a.Descripcion, ta.Nombre AS TipoArticulo,
                   a.CostoPromedio, a.StockMinimo, a.PuntoReorden, a.Estado,
                   ISNULL(stk.Existencias, 0) AS Existencias,
                   a.DiasVidaUtil, p.Fracciones,
                   CAST(CASE WHEN a.Imagen IS NULL THEN 0 ELSE 1 END AS BIT) AS TieneImagen,
                   a.Fracciona, a.PrecioVentaUnidad, a.Costo,
                   a.PPublico, a.PBodega, a.PCredito,
                   a.UPublico,  a.UBodega, a.UCredito,
                   a.MarcaCodigo, m.Marca AS MarcaNombre,
                   a.GrupoMenorCodigo, gm.Nombre AS GrupoMenorNombre,
                   gm.GrupoMayor AS GrupoMayorCodigo, gmay.Nombre AS GrupoMayorNombre,
                   a.PresentacionCodigo, p.Presentacion AS PresentacionNombre,
                   a.Peso, a.IvaSiNo, a.IvaValor, a.IvaDescripcion,
                   a.Iva2, a.IvaDescripcion2, a.TipoArticuloID " + sqlBase + @"
            ORDER BY a.Nombre
            OFFSET @Offset ROWS FETCH NEXT @Tamano ROWS ONLY",
            new { TipoArticuloId = tipoArticuloId, Texto = texto, Estado = estado, CentroCostoId = centroCostoId, Offset = offset, Tamano = tamano },
            commandTimeout: 60)).ToList();

        // Vista general (sin filtro CC): enriquecer con stock desglosado por CC.
        // Solo cuando hay más de un CC con Visions activo para que los chips tengan sentido.
        if (!centroCostoId.HasValue && items.Count > 0)
        {
            var ids = items.Select(i => i.ArticuloID).ToList();
            var stockPorCC = (await connection.QueryAsync<(int ArticuloID, int CentroCostoID, string NombreCC, decimal Stock)>(@"
                SELECT ist.ArticuloID, cc.CentroCostoID, cc.Nombre AS NombreCC,
                       SUM(ist.CantidadActual) AS Stock
                FROM Inventario.InventarioStock ist
                JOIN Inventario.Bodegas b        ON b.BodegaID    = ist.BodegaID
                JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = b.CentroCostoID
                WHERE ist.ArticuloID IN @Ids AND cc.TieneVisions = 1
                GROUP BY ist.ArticuloID, cc.CentroCostoID, cc.Nombre",
                new { Ids = ids }))
                .GroupBy(r => r.ArticuloID)
                .ToDictionary(g => g.Key, g => g.Select(r => new StockPorCC(r.CentroCostoID, r.NombreCC, r.Stock)).ToList());

            items = items.Select(a => stockPorCC.TryGetValue(a.ArticuloID, out var lista) && lista.Count > 1
                ? a with { StockPorCentros = lista }
                : a).ToList();
        }

        var resumenTipos = (await connection.QueryAsync<ResumenTipoItem>(@"
            SELECT ta.Nombre AS Tipo, COUNT(*) AS Cantidad
            FROM Catalogo.Tarjetas a
            JOIN Catalogo.TiposArticulo ta ON ta.TipoArticuloID = a.TipoArticuloID
            WHERE (@TipoArticuloId IS NULL OR a.TipoArticuloID = @TipoArticuloId)
              AND (@Texto IS NULL OR a.Nombre LIKE '%' + @Texto + '%' OR a.Referencia LIKE '%' + @Texto + '%')
              AND (@Estado IS NULL OR a.Estado = @Estado)
              AND (@CentroCostoId IS NULL OR EXISTS (
                  SELECT 1 FROM Integracion.MapeoArticulos ma
                  WHERE ma.ArticuloID = a.ArticuloID AND ma.CentroCostoID = @CentroCostoId AND ma.Estado = 1))
            GROUP BY ta.Nombre
            ORDER BY COUNT(*) DESC",
            p, commandTimeout: 60)).ToList();

        return new ArticulosPaginadosResponse(items, total, pagina, tamano, resumenTipos);
    }

    public async Task<int> ContarArticulosAsync(bool? estado = null)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Catalogo.Tarjetas WHERE (@Estado IS NULL OR Estado = @Estado)",
            new { Estado = estado });
    }

    public async Task<IEnumerable<ArticuloItem>> ListarTodosArticulosAsync(int? tipoArticuloId = null, bool? estado = null)
    {
        using var connection = _db.CreateConnection();

        const string sqlBase = @"
            FROM Catalogo.Tarjetas a
            JOIN Catalogo.TiposArticulo ta ON ta.TipoArticuloID = a.TipoArticuloID
            LEFT JOIN Catalogo.Marca m ON m.Codigo = a.MarcaCodigo
            LEFT JOIN Catalogo.GrupoMenor gm ON gm.Codigo = a.GrupoMenorCodigo
            LEFT JOIN Catalogo.GrupoMayor gmay ON gmay.Codigo = gm.GrupoMayor
            LEFT JOIN Catalogo.Presentacion p ON p.Codigo = a.PresentacionCodigo
            LEFT JOIN (
                SELECT ArticuloID, SUM(CantidadActual) AS Existencias
                FROM Inventario.InventarioStock
                GROUP BY ArticuloID
            ) stk ON stk.ArticuloID = a.ArticuloID
            WHERE (@TipoArticuloId IS NULL OR a.TipoArticuloID = @TipoArticuloId)
              AND (@Estado IS NULL OR a.Estado = @Estado)";

        return await connection.QueryAsync<ArticuloItem>(@"
            SELECT TOP 2000
                   a.ArticuloID, a.Referencia, a.Nombre, a.Descripcion, ta.Nombre AS TipoArticulo,
                   a.CostoPromedio, a.StockMinimo, a.PuntoReorden, a.Estado,
                   ISNULL(stk.Existencias, 0) AS Existencias,
                   a.DiasVidaUtil, p.Fracciones,
                   CAST(CASE WHEN a.Imagen IS NULL THEN 0 ELSE 1 END AS BIT) AS TieneImagen,
                   a.Fracciona, a.PrecioVentaUnidad, a.Costo,
                   a.PPublico, a.PBodega, a.PCredito,
                   a.UPublico,  a.UBodega, a.UCredito,
                   a.MarcaCodigo, m.Marca AS MarcaNombre,
                   a.GrupoMenorCodigo, gm.Nombre AS GrupoMenorNombre,
                   gm.GrupoMayor AS GrupoMayorCodigo, gmay.Nombre AS GrupoMayorNombre,
                   a.PresentacionCodigo, p.Presentacion AS PresentacionNombre,
                   a.Peso, a.IvaSiNo, a.IvaValor, a.IvaDescripcion,
                   a.Iva2, a.IvaDescripcion2, a.TipoArticuloID " + sqlBase + @"
            ORDER BY a.Nombre",
            new { TipoArticuloId = tipoArticuloId, Estado = estado },
            commandTimeout: 60);
    }

    public async Task<IEnumerable<ArticuloItem>> BuscarArticulosAsync(string? q, bool? soloTerminados = null, int max = 30)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<ArticuloItem>(@"
            SELECT TOP (@Max)
                   a.ArticuloID, a.Referencia, a.Nombre,
                   CAST(NULL AS NVARCHAR(MAX)) AS Descripcion,
                   ta.Nombre AS TipoArticulo,
                   a.CostoPromedio,
                   a.StockMinimo,
                   CAST(0 AS DECIMAL(18,2)) AS PuntoReorden,
                   a.Estado,
                   CAST(0 AS DECIMAL(18,2)) AS Existencias,
                   CAST(NULL AS INT) AS DiasVidaUtil,
                   p.Fracciones,
                   CAST(CASE WHEN a.Imagen IS NULL THEN 0 ELSE 1 END AS BIT) AS TieneImagen,
                   CAST(NULL AS NVARCHAR(50)) AS Fracciona,
                   CAST(NULL AS DECIMAL(18,2)) AS PrecioVentaUnidad,
                   CAST(NULL AS DECIMAL(18,2)) AS Costo,
                   a.PPublico,
                   CAST(NULL AS DECIMAL(18,2)) AS PBodega,
                   CAST(NULL AS DECIMAL(18,2)) AS PCredito,
                   CAST(NULL AS DECIMAL(18,2)) AS UPublico,
                   CAST(NULL AS DECIMAL(18,2)) AS UBodega,
                   CAST(NULL AS DECIMAL(18,2)) AS UCredito,
                   a.MarcaCodigo,
                   CAST(NULL AS NVARCHAR(200)) AS MarcaNombre,
                   a.GrupoMenorCodigo,
                   CAST(NULL AS NVARCHAR(200)) AS GrupoMenorNombre,
                   CAST(NULL AS NVARCHAR(50)) AS GrupoMayorCodigo,
                   CAST(NULL AS NVARCHAR(200)) AS GrupoMayorNombre,
                   a.PresentacionCodigo, p.Presentacion AS PresentacionNombre,
                   CAST(NULL AS DECIMAL(18,4)) AS Peso,
                   a.IvaSiNo, a.IvaValor, a.IvaDescripcion,
                   a.Iva2, a.IvaDescripcion2, a.TipoArticuloID
            FROM Catalogo.Tarjetas a
            JOIN Catalogo.TiposArticulo ta ON ta.TipoArticuloID = a.TipoArticuloID
            LEFT JOIN Catalogo.Presentacion p ON p.Codigo = a.PresentacionCodigo
            WHERE a.Estado = 1
              AND (@Q IS NULL OR a.Nombre LIKE '%' + @Q + '%' OR a.Referencia LIKE '%' + @Q + '%')
              AND (@SoloTerminados IS NULL
                   OR (@SoloTerminados = 1 AND ta.Codigo = 'PT')
                   OR (@SoloTerminados = 0 AND ta.Codigo <> 'PT'))
            ORDER BY a.Nombre",
            new { Max = max, Q = string.IsNullOrWhiteSpace(q) ? (string?)null : q, SoloTerminados = soloTerminados });
    }

    public async Task<ArticuloItem?> ObtenerArticuloAsync(int articuloId)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT a.ArticuloID, a.Referencia, a.Nombre, a.Descripcion, ta.Nombre AS TipoArticulo,
                   a.CostoPromedio, a.StockMinimo, a.PuntoReorden, a.Estado,
                   (SELECT ISNULL(SUM(ist.CantidadActual),0) FROM Inventario.InventarioStock ist WHERE ist.ArticuloID = a.ArticuloID) AS Existencias,
                   a.DiasVidaUtil,
                   p.Fracciones,
                   CAST(CASE WHEN a.Imagen IS NULL THEN 0 ELSE 1 END AS BIT) AS TieneImagen,
                   a.Fracciona, a.PrecioVentaUnidad,
                   a.Costo,
                   a.PPublico, a.PBodega, a.PCredito,
                   a.UPublico,  a.UBodega, a.UCredito,
                   a.MarcaCodigo, m.Marca AS MarcaNombre,
                   a.GrupoMenorCodigo, gm.Nombre AS GrupoMenorNombre,
                   gm.GrupoMayor AS GrupoMayorCodigo, gmay.Nombre AS GrupoMayorNombre,
                   a.PresentacionCodigo, p.Presentacion AS PresentacionNombre,
                   a.Peso, a.IvaSiNo, a.IvaValor, a.IvaDescripcion,
                   a.Iva2, a.IvaDescripcion2,
                   a.TipoArticuloID
            FROM Catalogo.Tarjetas a
            JOIN Catalogo.TiposArticulo ta ON ta.TipoArticuloID = a.TipoArticuloID
            LEFT JOIN Catalogo.Marca m ON m.Codigo = a.MarcaCodigo
            LEFT JOIN Catalogo.GrupoMenor gm ON gm.Codigo = a.GrupoMenorCodigo
            LEFT JOIN Catalogo.GrupoMayor gmay ON gmay.Codigo = gm.GrupoMayor
            LEFT JOIN Catalogo.Presentacion p ON p.Codigo = a.PresentacionCodigo
            WHERE a.ArticuloID = @ArticuloId";

        return await connection.QueryFirstOrDefaultAsync<ArticuloItem>(sql, new { ArticuloId = articuloId });
    }

    private record PreciosActuales(decimal? Costo, decimal? PPublico, decimal? PBodega, decimal? PCredito);

    public async Task ActualizarArticuloAsync(int articuloId, ActualizarArticuloRequest r, int usuarioId, string nombreUsuario)
    {
        using var connection = _db.CreateConnection();

        // ICO no admite segundo impuesto: validar antes de actualizar
        if (r.Iva2.HasValue && r.Iva2 != 0 && r.IvaValor.HasValue)
        {
            var esICO = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Catalogo.Iva WHERE Iva = @Iva AND ISNULL(TipoImpuesto,'IVA') = 'ICO'",
                new { Iva = r.IvaValor.Value });
            if (esICO > 0)
                throw new InvalidOperationException("El ICO solo puede ser impuesto principal. Un artículo con ICO no puede tener segundo impuesto.");
        }

        // Leer precios actuales para detectar cambios
        var precios = await connection.QuerySingleOrDefaultAsync<PreciosActuales>(
            "SELECT Costo, PPublico, PBodega, PCredito FROM Catalogo.Tarjetas WHERE ArticuloID = @Id",
            new { Id = articuloId });

        const string sql = @"
            UPDATE Catalogo.Tarjetas
            SET Nombre = @Nombre, Descripcion = @Descripcion,
                StockMinimo = @StockMinimo, PuntoReorden = @PuntoReorden, DiasVidaUtil = @DiasVidaUtil,
                Estado = @Estado,
                Fracciona = ISNULL(@Fracciona, 'SI'), PrecioVentaUnidad = @PrecioVentaUnidad,
                Costo = @Costo,
                PPublico = @PPublico, PBodega = @PBodega, PCredito = @PCredito,
                UPublico = @UPublico, UBodega = @UBodega, UCredito = @UCredito,
                MarcaCodigo = @MarcaCodigo, GrupoMenorCodigo = @GrupoMenorCodigo,
                PresentacionCodigo = @PresentacionCodigo,
                Peso = @Peso, IvaSiNo = @IvaSiNo, IvaValor = @IvaValor, IvaDescripcion = @IvaDescripcion,
                Iva2 = @Iva2, IvaDescripcion2 = @IvaDescripcion2,
                TipoArticuloID = ISNULL(@TipoArticuloId, TipoArticuloID),
                FechaModificacion = GETDATE()
            WHERE ArticuloID = @ArticuloId";

        var filas = await connection.ExecuteAsync(sql, new
        {
            ArticuloId = articuloId,
            r.Nombre, r.Descripcion, r.StockMinimo, r.PuntoReorden,
            r.DiasVidaUtil, r.Estado, r.Fracciona, r.PrecioVentaUnidad,
            r.Costo,
            r.PPublico, r.PBodega, r.PCredito,
            r.UPublico,  r.UBodega, r.UCredito,
            r.MarcaCodigo, r.GrupoMenorCodigo, r.PresentacionCodigo,
            r.Peso, r.IvaSiNo, r.IvaValor, r.IvaDescripcion,
            r.Iva2, r.IvaDescripcion2, r.TipoArticuloId
        });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el articulo {articuloId}.");

        // Registrar cambios de precio en historial
        if (precios is not null)
        {
            var cambios = new List<(string Campo, decimal? Antes, decimal? Despues)>();
            if (r.Costo     != precios.Costo)    cambios.Add(("Costo",    precios.Costo,    r.Costo));
            if (r.PPublico  != precios.PPublico)  cambios.Add(("PPublico", precios.PPublico, r.PPublico));
            if (r.PBodega   != precios.PBodega)   cambios.Add(("PBodega",  precios.PBodega,  r.PBodega));
            if (r.PCredito  != precios.PCredito)  cambios.Add(("PCredito", precios.PCredito, r.PCredito));

            foreach (var (campo, antes, despues) in cambios)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO Auditoria.HistorialPrecios
                        (ArticuloID, UsuarioID, NombreUsuario, Campo, ValorAnterior, ValorNuevo)
                      VALUES
                        (@ArticuloID, @UsuarioID, @NombreUsuario, @Campo, @ValorAnterior, @ValorNuevo)",
                    new { ArticuloID = articuloId, UsuarioID = usuarioId, NombreUsuario = nombreUsuario,
                          Campo = campo, ValorAnterior = antes, ValorNuevo = despues });
            }
        }

        await connection.ExecuteAsync(@"
            INSERT INTO Integracion.EventosSalientes (TipoEvento, CentroCostoID, ArticuloID, Cantidad, CostoUnitario)
            SELECT 'SINCRONIZAR_ARTICULO', ma.CentroCostoID, ma.ArticuloID, 0, ISNULL(a.CostoPromedio, 0)
            FROM Integracion.MapeoArticulos ma
            JOIN Catalogo.Tarjetas a          ON a.ArticuloID    = ma.ArticuloID
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ma.CentroCostoID
            WHERE ma.ArticuloID = @ArticuloId AND ma.Estado = 1 AND cc.TieneVisions = 1",
            new { ArticuloId = articuloId });
    }

    public async Task<IEnumerable<HistorialPrecioItem>> ListarHistorialPreciosAsync(int articuloId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<HistorialPrecioItem>(
            @"SELECT TOP 100 HistorialID, FechaCambio, NombreUsuario, Campo, ValorAnterior, ValorNuevo
              FROM Auditoria.HistorialPrecios
              WHERE ArticuloID = @ArticuloId
              ORDER BY FechaCambio DESC",
            new { ArticuloId = articuloId });
    }

    private record ImagenArticulo(byte[] Imagen, string ImagenContentType);

    public async Task<(byte[] Datos, string ContentType)?> ObtenerImagenArticuloAsync(int articuloId)
    {
        using var connection = _db.CreateConnection();

        var resultado = await connection.QuerySingleOrDefaultAsync<ImagenArticulo>(
            "SELECT Imagen, ImagenContentType FROM Catalogo.Tarjetas WHERE ArticuloID = @ArticuloId AND Imagen IS NOT NULL",
            new { ArticuloId = articuloId });

        return resultado is null ? null : (resultado.Imagen, resultado.ImagenContentType);
    }

    public async Task ActualizarImagenArticuloAsync(int articuloId, ActualizarImagenRequest r)
    {
        using var connection = _db.CreateConnection();
        var datos = Convert.FromBase64String(r.Base64);

        var filas = await connection.ExecuteAsync(
            "UPDATE Catalogo.Tarjetas SET Imagen = @Datos, ImagenContentType = @ContentType, FechaImagenActualizada = GETUTCDATE() WHERE ArticuloID = @ArticuloId",
            new { ArticuloId = articuloId, Datos = datos, r.ContentType });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el articulo {articuloId}.");
    }

    public async Task EliminarImagenArticuloAsync(int articuloId)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Catalogo.Tarjetas SET Imagen = NULL, ImagenContentType = NULL WHERE ArticuloID = @ArticuloId",
            new { ArticuloId = articuloId });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el articulo {articuloId}.");
    }

    // ================= Iva =================

    public async Task<IEnumerable<IvaItem>> ListarIvasAsync()
    {
        if (_cache.TryGetValue("cat:ivas", out IEnumerable<IvaItem>? cached)) return cached!;
        using var connection = _db.CreateConnection();
        var result = (await connection.QueryAsync<IvaItem>(
            "SELECT IvaID, Iva, Descripcion, ISNULL(TipoImpuesto,'IVA') AS TipoImpuesto FROM Catalogo.Iva ORDER BY TipoImpuesto, Iva, Descripcion")).ToList();
        _cache.Set("cat:ivas", result, _ttlCatalogos);
        return result;
    }

    public async Task<int> CrearIvaAsync(CrearIvaRequest r)
    {
        using var connection = _db.CreateConnection();
        var tipo = string.IsNullOrWhiteSpace(r.TipoImpuesto) ? "IVA" : r.TipoImpuesto.ToUpper();
        var id = await connection.ExecuteScalarAsync<int>(
            "INSERT INTO Catalogo.Iva (Iva, Descripcion, TipoImpuesto) OUTPUT INSERTED.IvaID VALUES (@Iva, @Descripcion, @Tipo)",
            new { r.Iva, r.Descripcion, Tipo = tipo });
        _cache.Remove("cat:ivas");
        return id;
    }

    public async Task ActualizarIvaAsync(int ivaId, ActualizarIvaRequest r)
    {
        using var connection = _db.CreateConnection();
        var tipo = string.IsNullOrWhiteSpace(r.TipoImpuesto) ? "IVA" : r.TipoImpuesto.ToUpper();
        var filas = await connection.ExecuteAsync(
            "UPDATE Catalogo.Iva SET Iva = @Iva, Descripcion = @Descripcion, TipoImpuesto = @Tipo WHERE IvaID = @IvaID",
            new { IvaID = ivaId, r.Iva, r.Descripcion, Tipo = tipo });
        if (filas == 0) throw new KeyNotFoundException($"Impuesto {ivaId} no encontrado.");
        _cache.Remove("cat:ivas");
    }

    public async Task EliminarIvaAsync(int ivaId)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "DELETE FROM Catalogo.Iva WHERE IvaID = @IvaID", new { IvaID = ivaId });
        if (filas == 0) throw new KeyNotFoundException($"IVA {ivaId} no encontrado.");
        _cache.Remove("cat:ivas");
    }

    // ================= GruposMayores =================

    public async Task<IEnumerable<GrupoMayorItem>> ListarGruposMayoresAsync()
    {
        if (_cache.TryGetValue("cat:grupos-mayor", out IEnumerable<GrupoMayorItem>? cached)) return cached!;
        using var connection = _db.CreateConnection();
        var result = (await connection.QueryAsync<GrupoMayorItem>(
            "SELECT Codigo, Nombre FROM Catalogo.GrupoMayor ORDER BY Nombre")).ToList();
        _cache.Set("cat:grupos-mayor", result, _ttlCatalogos);
        return result;
    }

    public async Task CrearGrupoMayorAsync(CrearGrupoMayorRequest r)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO Catalogo.GrupoMayor (Codigo, Nombre) VALUES (@Codigo, @Nombre)", r);
        _cache.Remove("cat:grupos-mayor"); _cache.Remove("cat:grupos-menor");
    }

    public async Task ActualizarGrupoMayorAsync(string codigo, ActualizarGrupoMayorRequest r)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "UPDATE Catalogo.GrupoMayor SET Nombre = @Nombre WHERE Codigo = @Codigo",
            new { Codigo = codigo, r.Nombre });
        if (filas == 0) throw new KeyNotFoundException($"GrupoMayor '{codigo}' no encontrado.");
        _cache.Remove("cat:grupos-mayor"); _cache.Remove("cat:grupos-menor");
    }

    public async Task EliminarGrupoMayorAsync(string codigo)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "DELETE FROM Catalogo.GrupoMayor WHERE Codigo = @Codigo", new { Codigo = codigo });
        if (filas == 0) throw new KeyNotFoundException($"GrupoMayor '{codigo}' no encontrado.");
        _cache.Remove("cat:grupos-mayor"); _cache.Remove("cat:grupos-menor");
    }

    // ================= GruposMenores =================

    public async Task<IEnumerable<GrupoMenorItem>> ListarGruposMenoresAsync(string? grupoMayor)
    {
        var key = $"cat:grupos-menor:{grupoMayor ?? "all"}";
        if (_cache.TryGetValue(key, out IEnumerable<GrupoMenorItem>? cached)) return cached!;
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT gm.Codigo, gm.Nombre, gm.GrupoMayor, gmay.Nombre AS GrupoMayorNombre
            FROM Catalogo.GrupoMenor gm
            JOIN Catalogo.GrupoMayor gmay ON gmay.Codigo = gm.GrupoMayor
            WHERE (@GrupoMayor IS NULL OR gm.GrupoMayor = @GrupoMayor)
            ORDER BY gmay.Nombre, gm.Nombre";
        var result = (await connection.QueryAsync<GrupoMenorItem>(sql, new { GrupoMayor = grupoMayor })).ToList();
        _cache.Set(key, result, _ttlCatalogos);
        return result;
    }

    public async Task CrearGrupoMenorAsync(CrearGrupoMenorRequest r)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO Catalogo.GrupoMenor (Codigo, Nombre, GrupoMayor) VALUES (@Codigo, @Nombre, @GrupoMayor)", r);
        _cache.Remove("cat:grupos-menor:all"); _cache.Remove($"cat:grupos-menor:{r.GrupoMayor}");
    }

    public async Task ActualizarGrupoMenorAsync(string codigo, string grupoMayor, ActualizarGrupoMenorRequest r)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "UPDATE Catalogo.GrupoMenor SET Nombre = @Nombre WHERE Codigo = @Codigo AND GrupoMayor = @GrupoMayor",
            new { Codigo = codigo, GrupoMayor = grupoMayor, r.Nombre });
        if (filas == 0) throw new KeyNotFoundException($"GrupoMenor '{codigo}' / '{grupoMayor}' no encontrado.");
        _cache.Remove("cat:grupos-menor:all"); _cache.Remove($"cat:grupos-menor:{grupoMayor}");
    }

    public async Task EliminarGrupoMenorAsync(string codigo, string grupoMayor)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "DELETE FROM Catalogo.GrupoMenor WHERE Codigo = @Codigo AND GrupoMayor = @GrupoMayor",
            new { Codigo = codigo, GrupoMayor = grupoMayor });
        if (filas == 0) throw new KeyNotFoundException($"GrupoMenor '{codigo}' / '{grupoMayor}' no encontrado.");
        _cache.Remove("cat:grupos-menor:all"); _cache.Remove($"cat:grupos-menor:{grupoMayor}");
    }

    // ================= Marcas =================

    public async Task<IEnumerable<MarcaItem>> ListarMarcasAsync()
    {
        if (_cache.TryGetValue("cat:marcas", out IEnumerable<MarcaItem>? cached)) return cached!;
        using var connection = _db.CreateConnection();
        var result = (await connection.QueryAsync<MarcaItem>(
            "SELECT Codigo, Marca AS Nombre FROM Catalogo.Marca ORDER BY Marca")).ToList();
        _cache.Set("cat:marcas", result, _ttlCatalogos);
        return result;
    }

    public async Task CrearMarcaAsync(CrearMarcaRequest r)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO Catalogo.Marca (Codigo, Marca) VALUES (@Codigo, @Nombre)", r);
        _cache.Remove("cat:marcas");
    }

    public async Task ActualizarMarcaAsync(string codigo, ActualizarMarcaRequest r)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "UPDATE Catalogo.Marca SET Marca = @Nombre WHERE Codigo = @Codigo",
            new { Codigo = codigo, r.Nombre });
        if (filas == 0) throw new KeyNotFoundException($"Marca '{codigo}' no encontrada.");
        _cache.Remove("cat:marcas");
    }

    public async Task EliminarMarcaAsync(string codigo)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "DELETE FROM Catalogo.Marca WHERE Codigo = @Codigo", new { Codigo = codigo });
        if (filas == 0) throw new KeyNotFoundException($"Marca '{codigo}' no encontrada.");
        _cache.Remove("cat:marcas");
    }

    // ================= Presentaciones =================

    public async Task<IEnumerable<PresentacionItem>> ListarPresentacionesAsync()
    {
        if (_cache.TryGetValue("cat:presentaciones", out IEnumerable<PresentacionItem>? cached)) return cached!;
        using var connection = _db.CreateConnection();
        var result = (await connection.QueryAsync<PresentacionItem>(
            "SELECT Codigo, Presentacion, Fracciones, Tipo FROM Catalogo.Presentacion ORDER BY Presentacion")).ToList();
        _cache.Set("cat:presentaciones", result, _ttlCatalogos);
        return result;
    }

    public async Task CrearPresentacionAsync(CrearPresentacionRequest r)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO Catalogo.Presentacion (Codigo, Presentacion, Fracciones, Tipo) VALUES (@Codigo, @Presentacion, @Fracciones, @Tipo)", r);
        _cache.Remove("cat:presentaciones");
    }

    public async Task ActualizarPresentacionAsync(string codigo, ActualizarPresentacionRequest r)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "UPDATE Catalogo.Presentacion SET Presentacion = @Presentacion, Fracciones = @Fracciones, Tipo = @Tipo WHERE Codigo = @Codigo",
            new { Codigo = codigo, r.Presentacion, r.Fracciones, r.Tipo });
        if (filas == 0) throw new KeyNotFoundException($"Presentacion '{codigo}' no encontrada.");
        _cache.Remove("cat:presentaciones");
    }

    public async Task EliminarPresentacionAsync(string codigo)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.ExecuteAsync(
            "DELETE FROM Catalogo.Presentacion WHERE Codigo = @Codigo", new { Codigo = codigo });
        if (filas == 0) throw new KeyNotFoundException($"Presentacion '{codigo}' no encontrada.");
        _cache.Remove("cat:presentaciones");
    }

    public async Task<IEnumerable<CentroTrabajoItem>> ListarCentrosTrabajoAsync(bool soloActivos)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT ct.CentroTrabajoID, ct.Nombre, ct.CentroCostoID, cc.Nombre AS CentroCosto,
                   ct.CostoHoraManoObra, ct.CostoHoraCIF, ct.Estado
            FROM Organizacion.CentrosTrabajo ct
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ct.CentroCostoID
            WHERE (@SoloActivos = 0 OR ct.Estado = 1)
            ORDER BY cc.Nombre, ct.Nombre";

        return await connection.QueryAsync<CentroTrabajoItem>(sql, new { SoloActivos = soloActivos });
    }

    public async Task<int> CrearCentroTrabajoAsync(CrearCentroTrabajoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Organizacion.CentrosTrabajo (Nombre, CentroCostoID, CostoHoraManoObra, CostoHoraCIF, Estado)
            OUTPUT INSERTED.CentroTrabajoID
            VALUES (@Nombre, @CentroCostoID, @CostoHoraManoObra, @CostoHoraCIF, 1)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task ActualizarCentroTrabajoAsync(int centroTrabajoId, ActualizarCentroTrabajoRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            UPDATE Organizacion.CentrosTrabajo
            SET Nombre = @Nombre, CostoHoraManoObra = @CostoHoraManoObra, CostoHoraCIF = @CostoHoraCIF, Estado = @Estado
            WHERE CentroTrabajoID = @CentroTrabajoId";

        var filas = await connection.ExecuteAsync(sql, new
        {
            CentroTrabajoId = centroTrabajoId,
            r.Nombre,
            r.CostoHoraManoObra,
            r.CostoHoraCIF,
            r.Estado
        });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el centro de trabajo {centroTrabajoId}.");
    }

    public async Task<IEnumerable<ProveedorItem>> ListarProveedoresAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<ProveedorItem>(
            @"SELECT p.ProveedorID, p.RazonSocial, p.NIT, p.Contacto, p.Telefono, p.Email, p.Direccion, p.Estado,
                     p.TipoPersona, p.PrimerNombre, p.SegundoNombre, p.PrimerApellido, p.SegundoApellido,
                     p.TipoIdentificacion, p.DigitoVerificacion, p.Departamento, p.Ciudad,
                     p.CodigoDept, p.CodigoMuni, p.Pais, p.CodigoPais,
                     ti.Detalle AS TipoIdentificacionDetalle
              FROM Catalogo.Proveedores p
              LEFT JOIN Catalogo.TiposIdentificacion ti ON ti.Codigo = p.TipoIdentificacion
              ORDER BY p.RazonSocial");
    }

    public async Task<ProveedorItem?> ObtenerProveedorAsync(int proveedorId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProveedorItem>(
            @"SELECT p.ProveedorID, p.RazonSocial, p.NIT, p.Contacto, p.Telefono, p.Email, p.Direccion, p.Estado,
                     p.TipoPersona, p.PrimerNombre, p.SegundoNombre, p.PrimerApellido, p.SegundoApellido,
                     p.TipoIdentificacion, p.DigitoVerificacion, p.Departamento, p.Ciudad,
                     p.CodigoDept, p.CodigoMuni, p.Pais, p.CodigoPais,
                     ti.Detalle AS TipoIdentificacionDetalle
              FROM Catalogo.Proveedores p
              LEFT JOIN Catalogo.TiposIdentificacion ti ON ti.Codigo = p.TipoIdentificacion
              WHERE p.ProveedorID = @proveedorId",
            new { proveedorId });
    }

    public async Task<int> CrearProveedorAsync(CrearProveedorRequest r)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            INSERT INTO Catalogo.Proveedores
                (RazonSocial, NIT, Contacto, Telefono, Email, Direccion,
                 TipoPersona, PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido,
                 TipoIdentificacion, DigitoVerificacion, Departamento, Ciudad,
                 CodigoDept, CodigoMuni, Pais, CodigoPais, FechaModificacion)
            OUTPUT INSERTED.ProveedorID
            VALUES
                (@RazonSocial, @NIT, @Contacto, @Telefono, @Email, @Direccion,
                 @TipoPersona, @PrimerNombre, @SegundoNombre, @PrimerApellido, @SegundoApellido,
                 @TipoIdentificacion, @DigitoVerificacion, @Departamento, @Ciudad,
                 @CodigoDept, @CodigoMuni, ISNULL(@Pais,'COLOMBIA'), ISNULL(@CodigoPais,'CO'), GETDATE())";
        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task ActualizarProveedorAsync(int proveedorId, ActualizarProveedorRequest r)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            UPDATE Catalogo.Proveedores SET
                RazonSocial = @RazonSocial, NIT = @NIT, Contacto = @Contacto, Telefono = @Telefono,
                Email = @Email, Direccion = @Direccion, Estado = @Estado,
                TipoPersona = @TipoPersona, PrimerNombre = @PrimerNombre, SegundoNombre = @SegundoNombre,
                PrimerApellido = @PrimerApellido, SegundoApellido = @SegundoApellido,
                TipoIdentificacion = @TipoIdentificacion, DigitoVerificacion = @DigitoVerificacion,
                Departamento = @Departamento, Ciudad = @Ciudad,
                CodigoDept = @CodigoDept, CodigoMuni = @CodigoMuni,
                Pais = ISNULL(@Pais,'COLOMBIA'), CodigoPais = ISNULL(@CodigoPais,'CO'),
                FechaModificacion = GETDATE()
            WHERE ProveedorID = @ProveedorId";
        var filas = await connection.ExecuteAsync(sql, new
        {
            ProveedorId = proveedorId,
            r.RazonSocial, r.NIT, r.Contacto, r.Telefono, r.Email, r.Direccion, r.Estado,
            r.TipoPersona, r.PrimerNombre, r.SegundoNombre, r.PrimerApellido, r.SegundoApellido,
            r.TipoIdentificacion, r.DigitoVerificacion, r.Departamento, r.Ciudad,
            r.CodigoDept, r.CodigoMuni, r.Pais, r.CodigoPais
        });
        if (filas == 0) throw new KeyNotFoundException($"No existe el proveedor {proveedorId}.");
    }

    public async Task<DualRolInfo> VerificarDualRolAsync(string nit)
    {
        using var connection = _db.CreateConnection();
        var clienteRow = await connection.QuerySingleOrDefaultAsync(
            "SELECT TOP 1 ClienteID, ExternalId FROM Crm.Clientes WHERE NIT = @NIT", new { NIT = nit });
        int? clienteId = clienteRow?.ClienteID;
        string? clienteExternalId = clienteRow?.ExternalId;
        var proveedorId = await connection.ExecuteScalarAsync<int?>(
            "SELECT TOP 1 ProveedorID FROM Catalogo.Proveedores WHERE NIT = @NIT", new { NIT = nit });
        return new DualRolInfo(clienteId.HasValue, clienteId, clienteExternalId, proveedorId.HasValue, proveedorId);
    }

    public async Task<int> AgregarComoProveedorDesdeClienteAsync(int clienteId)
    {
        using var connection = _db.CreateConnection();

        var c = await connection.QuerySingleOrDefaultAsync<dynamic>(
            @"SELECT c.ClienteID, c.NIT, c.Nombre, c.Telefono, c.Email, c.Direccion,
                     c.TipoPersona, c.PrimerNombre, c.SegundoNombre, c.PrimerApellido, c.SegundoApellido,
                     c.TipoIdentificacion, c.DigitoVerificacion, c.Departamento, c.Ciudad,
                     c.CodigoDept, c.CodigoMuni, c.Pais, c.CodigoPais
              FROM Crm.Clientes c WHERE c.ClienteID = @ClienteID",
            new { ClienteID = clienteId });

        if (c == null) throw new KeyNotFoundException($"No existe el cliente {clienteId}.");

        // Si ya existe como proveedor con ese NIT, solo retornar el ID existente
        var existente = await connection.ExecuteScalarAsync<int?>(
            "SELECT TOP 1 ProveedorID FROM Catalogo.Proveedores WHERE NIT = @NIT",
            new { NIT = (string)c.NIT });
        if (existente.HasValue) return existente.Value;

        return await connection.ExecuteScalarAsync<int>(@"
            INSERT INTO Catalogo.Proveedores
                (RazonSocial, NIT, Telefono, Email, Direccion,
                 TipoPersona, PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido,
                 TipoIdentificacion, DigitoVerificacion, Departamento, Ciudad,
                 CodigoDept, CodigoMuni, Pais, CodigoPais, Estado, FechaModificacion)
            OUTPUT INSERTED.ProveedorID
            VALUES
                (@Nombre, @NIT, @Telefono, @Email, @Direccion,
                 @TipoPersona, @PrimerNombre, @SegundoNombre, @PrimerApellido, @SegundoApellido,
                 @TipoIdentificacion, @DigitoVerificacion, @Departamento, @Ciudad,
                 @CodigoDept, @CodigoMuni, ISNULL(@Pais,'COLOMBIA'), ISNULL(@CodigoPais,'CO'), 1, GETDATE())",
            new
            {
                Nombre = (string)c.Nombre, NIT = (string)c.NIT,
                Telefono = (string?)c.Telefono, Email = (string?)c.Email, Direccion = (string?)c.Direccion,
                TipoPersona = (string?)c.TipoPersona, PrimerNombre = (string?)c.PrimerNombre,
                SegundoNombre = (string?)c.SegundoNombre, PrimerApellido = (string?)c.PrimerApellido,
                SegundoApellido = (string?)c.SegundoApellido,
                TipoIdentificacion = (string?)c.TipoIdentificacion, DigitoVerificacion = (int?)c.DigitoVerificacion,
                Departamento = (string?)c.Departamento, Ciudad = (string?)c.Ciudad,
                CodigoDept = (string?)c.CodigoDept, CodigoMuni = (string?)c.CodigoMuni,
                Pais = (string?)c.Pais, CodigoPais = (string?)c.CodigoPais
            });
    }

    public async Task<int> AgregarComoClienteDesdeProveedorAsync(int proveedorId)
    {
        using var connection = _db.CreateConnection();

        var p = await connection.QuerySingleOrDefaultAsync<dynamic>(
            @"SELECT ProveedorID, NIT, RazonSocial, Telefono, Email, Direccion,
                     TipoPersona, PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido,
                     TipoIdentificacion, DigitoVerificacion, Departamento, Ciudad,
                     CodigoDept, CodigoMuni, Pais, CodigoPais
              FROM Catalogo.Proveedores WHERE ProveedorID = @ProveedorID",
            new { ProveedorID = proveedorId });

        if (p == null) throw new KeyNotFoundException($"No existe el proveedor {proveedorId}.");

        // Si ya existe como cliente con ese NIT, solo retornar el ID existente
        var existente = await connection.ExecuteScalarAsync<int?>(
            "SELECT TOP 1 ClienteID FROM Crm.Clientes WHERE NIT = @NIT",
            new { NIT = (string)p.NIT });
        if (existente.HasValue) return existente.Value;

        var tipoPersona = (string?)p.TipoPersona ?? "Juridica";
        var tipoCliente  = tipoPersona == "Natural" ? "Persona Natural" : "Empresa";

        return await connection.ExecuteScalarAsync<int>(@"
            INSERT INTO Crm.Clientes
                (Nombre, NIT, Telefono, Email, Direccion, TipoPersona, TipoCliente,
                 PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido,
                 TipoIdentificacion, DigitoVerificacion, Departamento, Ciudad,
                 CodigoDept, CodigoMuni, Pais, CodigoPais, Estado, FechaCreacion, FechaModificacion)
            OUTPUT INSERTED.ClienteID
            VALUES
                (@RazonSocial, @NIT, @Telefono, @Email, @Direccion, @TipoPersona, @TipoCliente,
                 @PrimerNombre, @SegundoNombre, @PrimerApellido, @SegundoApellido,
                 @TipoIdentificacion, @DigitoVerificacion, @Departamento, @Ciudad,
                 @CodigoDept, @CodigoMuni, ISNULL(@Pais,'COLOMBIA'), ISNULL(@CodigoPais,'CO'),
                 1, GETDATE(), GETDATE())",
            new
            {
                RazonSocial = (string)p.RazonSocial, NIT = (string)p.NIT,
                Telefono = (string?)p.Telefono, Email = (string?)p.Email, Direccion = (string?)p.Direccion,
                TipoPersona = tipoPersona, TipoCliente = tipoCliente,
                PrimerNombre = (string?)p.PrimerNombre, SegundoNombre = (string?)p.SegundoNombre,
                PrimerApellido = (string?)p.PrimerApellido, SegundoApellido = (string?)p.SegundoApellido,
                TipoIdentificacion = (string?)p.TipoIdentificacion, DigitoVerificacion = (int?)p.DigitoVerificacion,
                Departamento = (string?)p.Departamento, Ciudad = (string?)p.Ciudad,
                CodigoDept = (string?)p.CodigoDept, CodigoMuni = (string?)p.CodigoMuni,
                Pais = (string?)p.Pais, CodigoPais = (string?)p.CodigoPais
            });
    }

    public async Task<IEnumerable<NexoApi.Features.Crm.Dtos.EventoHistorialProveedorItem>> ObtenerHistorialProveedorAsync(int proveedorId)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT 'ORDEN_COMPRA' AS TipoEvento, oc.FechaEmision AS Fecha,
                   'OC-' + RIGHT('000000' + CAST(oc.OrdenCompraID AS VARCHAR), 6) AS Titulo,
                   oc.EstadoOC + ISNULL(' · ' + b.Nombre, '') AS Detalle
            FROM Compras.OrdenesCompra oc
            LEFT JOIN Inventario.Bodegas b ON b.BodegaID = oc.BodegaDestinoID
            WHERE oc.ProveedorID = @ProveedorID

            UNION ALL

            SELECT 'DOCUMENTO' AS TipoEvento, d.FechaGeneracion AS Fecha,
                   d.Titulo AS Titulo,
                   'Plantilla: ' + p.Nombre AS Detalle
            FROM Documentos.DocumentosGenerados d
            JOIN Documentos.PlantillasDocumento p ON p.PlantillaID = d.PlantillaID
            WHERE d.ProveedorID = @ProveedorID

            ORDER BY Fecha DESC";

        return await connection.QueryAsync<NexoApi.Features.Crm.Dtos.EventoHistorialProveedorItem>(
            sql, new { ProveedorID = proveedorId });
    }

    public async Task<IEnumerable<TipoArticuloItem>> ListarTiposArticuloAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<TipoArticuloItem>(
            "SELECT TipoArticuloID, Codigo, Nombre FROM Catalogo.TiposArticulo WHERE Codigo IN ('IN','SER','MP','PT') ORDER BY Nombre");
    }

    public async Task<IEnumerable<UnidadMedidaItem>> ListarUnidadesMedidaAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<UnidadMedidaItem>(
            "SELECT UnidadID, Nombre, Abreviatura, Tipo FROM Catalogo.UnidadesMedida ORDER BY Nombre");
    }

    // ================= Variantes =================

    public async Task<IEnumerable<VarianteItem>> ListarVariantesAsync(int padreId)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<VarianteItem>(
            @"SELECT t.ArticuloID, t.Referencia, t.Nombre, t.NombreVariante, t.Estado,
                     ISNULL(SUM(s.CantidadActual), 0) AS Existencias
              FROM Catalogo.Tarjetas t
              LEFT JOIN Inventario.vw_StockConsolidado s ON s.ArticuloID = t.ArticuloID
              WHERE t.ArticuloPadreID = @PadreId
              GROUP BY t.ArticuloID, t.Referencia, t.Nombre, t.NombreVariante, t.Estado
              ORDER BY t.NombreVariante, t.Nombre",
            new { PadreId = padreId });
    }

    public async Task<int> CrearVarianteAsync(int padreId, CrearVarianteRequest r)
    {
        using var connection = _db.CreateConnection();

        var padre = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT Referencia, Nombre, Descripcion, TipoArticuloID, StockMinimo, PuntoReorden, " +
            "DiasVidaUtil, Fracciona, PrecioVentaUnidad, Costo, PPublico, PBodega, PCredito, " +
            "UPublico, UBodega, UCredito, MarcaCodigo, GrupoMenorCodigo, PresentacionCodigo, " +
            "Peso, IvaSiNo, IvaValor, IvaDescripcion, Iva2, IvaDescripcion2 " +
            "FROM Catalogo.Tarjetas WHERE ArticuloID = @PadreId",
            new { PadreId = padreId })
            ?? throw new KeyNotFoundException($"Artículo padre {padreId} no encontrado.");

        var referencia = r.Referencia ?? ($"{padre.Referencia}-V{NexoApi.Common.ColombiaClock.Now:MMddHHmm}");
        var nombre     = r.Nombre     ?? (string)padre.Nombre;

        return await connection.ExecuteScalarAsync<int>(@"
            INSERT INTO Catalogo.Tarjetas
                (Referencia, Nombre, Descripcion, TipoArticuloID, StockMinimo, PuntoReorden,
                 DiasVidaUtil, Fracciona, PrecioVentaUnidad, Costo, PPublico, PBodega, PCredito,
                 UPublico, UBodega, UCredito, MarcaCodigo, GrupoMenorCodigo, PresentacionCodigo,
                 Peso, IvaSiNo, IvaValor, IvaDescripcion, Iva2, IvaDescripcion2,
                 ArticuloPadreID, NombreVariante)
            OUTPUT INSERTED.ArticuloID
            VALUES
                (@Referencia, @Nombre, @Descripcion, @TipoArticuloID, @StockMinimo, @PuntoReorden,
                 @DiasVidaUtil, @Fracciona, @PrecioVentaUnidad, @Costo, @PPublico, @PBodega, @PCredito,
                 @UPublico, @UBodega, @UCredito, @MarcaCodigo, @GrupoMenorCodigo, @PresentacionCodigo,
                 @Peso, @IvaSiNo, @IvaValor, @IvaDescripcion, @Iva2, @IvaDescripcion2,
                 @ArticuloPadreID, @NombreVariante)",
            new
            {
                Referencia = referencia, Nombre = nombre,
                Descripcion = (object?)padre.Descripcion ?? DBNull.Value,
                padre.TipoArticuloID, padre.StockMinimo, padre.PuntoReorden,
                DiasVidaUtil = (object?)padre.DiasVidaUtil ?? DBNull.Value,
                Fracciona = (object?)padre.Fracciona ?? "SI",
                PrecioVentaUnidad = (object?)padre.PrecioVentaUnidad ?? DBNull.Value,
                Costo = (object?)padre.Costo ?? DBNull.Value,
                PPublico = (object?)padre.PPublico ?? DBNull.Value,
                PBodega = (object?)padre.PBodega ?? DBNull.Value,
                PCredito = (object?)padre.PCredito ?? DBNull.Value,
                UPublico = (object?)padre.UPublico ?? DBNull.Value,
                UBodega = (object?)padre.UBodega ?? DBNull.Value,
                UCredito = (object?)padre.UCredito ?? DBNull.Value,
                MarcaCodigo = (object?)padre.MarcaCodigo ?? DBNull.Value,
                GrupoMenorCodigo = (object?)padre.GrupoMenorCodigo ?? DBNull.Value,
                PresentacionCodigo = (object?)padre.PresentacionCodigo ?? DBNull.Value,
                Peso = (object?)padre.Peso ?? DBNull.Value,
                IvaSiNo = (object?)padre.IvaSiNo ?? DBNull.Value,
                IvaValor = (object?)padre.IvaValor ?? DBNull.Value,
                IvaDescripcion = (object?)padre.IvaDescripcion ?? DBNull.Value,
                Iva2 = (object?)padre.Iva2 ?? DBNull.Value,
                IvaDescripcion2 = (object?)padre.IvaDescripcion2 ?? DBNull.Value,
                ArticuloPadreID = padreId,
                NombreVariante = (object?)r.NombreVariante ?? DBNull.Value
            });
    }
}