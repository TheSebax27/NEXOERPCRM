using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Integracion.Dtos;
using NexoApi.Features.Marketing.Dtos;

namespace NexoApi.Features.Marketing;

public interface IComboService
{
    Task<IEnumerable<ComboItem>> ListarAsync(string? estado);
    Task<IEnumerable<ComboItem>> ListarActivosAsync();
    Task<ComboDetalle?> ObtenerAsync(int id);
    Task<int> CrearAsync(CrearComboRequest r, int usuarioId);
    Task ActualizarAsync(int id, ActualizarComboRequest r);
    Task<decimal?> CalcularPrecioAsync(int id);

    // Sync NEXO → VISIONS
    Task<IEnumerable<ComboItem>> ListarParaVisionsAsync(DateTime? desde);
    Task MarcarExportadoVisionsAsync(int comboId, int codigoVisions, string referenciaVisions);

    // Sync VISIONS → NEXO
    Task<int> SyncDesdeVisionsAsync(SyncComboDesdeVisionsRequest r);
    Task EliminarComboAsync(int id);
    Task EliminarComboVisionsAsync(int codigoVisions);
}

public class ComboService : IComboService
{
    private readonly IDbConnectionFactory _db;
    public ComboService(IDbConnectionFactory db) => _db = db;

    private record ItemCrudo(
        int ComboItemID, int ComboID, int ArticuloID, string SKU, string NombreArticulo,
        decimal Cantidad, string? Unidad, decimal? PrecioUnitarioSnapshot, decimal? PrecioVenta
    );

    private record DetalleRow(
        int ComboID, string Nombre, string? Descripcion,
        string? ImagenBase64, string? ImagenContentType,
        string Estado, string ModoPrecio,
        DateTime? FechaInicio, DateTime? FechaFin,
        decimal? PrecioManual, decimal PorcentajeDescuento,
        DateTime FechaCreacion,
        string Origen, int? CodigoVisions, string? ReferenciaVisions, bool ExportadoVisions
    );

    private record ListaRow(
        int ComboID, string Nombre, string? Descripcion, string Estado, string ModoPrecio,
        DateTime? FechaInicio, DateTime? FechaFin, decimal? PrecioManual, decimal PorcentajeDescuento,
        string? ImagenBase64, string? ImagenContentType,
        DateTime FechaCreacion, decimal? PrecioCalculado,
        string Origen, int? CodigoVisions, string? ReferenciaVisions, bool ExportadoVisions
    );

    private static ComboItem RowToItem(ListaRow r, IEnumerable<ItemCrudo> items) => new(
        r.ComboID, r.Nombre, r.Descripcion, r.Estado, r.ModoPrecio,
        r.FechaInicio.HasValue ? DateOnly.FromDateTime(r.FechaInicio.Value) : null,
        r.FechaFin.HasValue    ? DateOnly.FromDateTime(r.FechaFin.Value)    : null,
        r.PrecioManual, r.PorcentajeDescuento, r.PrecioCalculado,
        r.ImagenBase64, r.ImagenContentType, r.FechaCreacion,
        items.Select(i => new ComboItemLine(i.ComboItemID, i.ArticuloID, i.SKU, i.NombreArticulo, i.Cantidad, i.Unidad, i.PrecioUnitarioSnapshot)).ToList(),
        r.Origen, r.CodigoVisions, r.ReferenciaVisions, r.ExportadoVisions
    );

    private const string SqlItems = @"
        SELECT ci.ComboItemID, ci.ComboID, ci.ArticuloID, a.Referencia AS SKU, a.Nombre AS NombreArticulo,
               ci.Cantidad, a.PresentacionCodigo AS Unidad, ci.PrecioUnitarioSnapshot, a.PPublico AS PrecioVenta
        FROM Marketing.ComboItems ci
        JOIN Catalogo.Tarjetas a ON a.ArticuloID = ci.ArticuloID
        WHERE ci.ComboID IN @Ids";

    private static async Task<List<ItemCrudo>> CargarItemsAsync(
        System.Data.IDbConnection con, IEnumerable<int> ids)
    {
        var arr = ids.ToArray();
        if (arr.Length == 0) return [];
        return (await con.QueryAsync<ItemCrudo>(SqlItems, new { Ids = arr })).ToList();
    }

    public async Task<IEnumerable<ComboItem>> ListarAsync(string? estado)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT c.ComboID, c.Nombre, c.Descripcion, c.Estado, c.ModoPrecio,
                   c.FechaInicio, c.FechaFin, c.PrecioManual, c.PorcentajeDescuento,
                   c.ImagenBase64, c.ImagenContentType,
                   c.FechaCreacion,
                   (SELECT SUM(a.PPublico * ci.Cantidad) * (1 - c.PorcentajeDescuento / 100.0)
                    FROM Marketing.ComboItems ci
                    JOIN Catalogo.Tarjetas a ON a.ArticuloID = ci.ArticuloID
                    WHERE ci.ComboID = c.ComboID) AS PrecioCalculado,
                   c.Origen, c.CodigoVisions, c.ReferenciaVisions, c.ExportadoVisions
            FROM Marketing.Combos c
            WHERE (@Estado IS NULL OR c.Estado = @Estado)
            ORDER BY c.FechaCreacion DESC";

        var rows = (await con.QueryAsync<ListaRow>(sql, new { Estado = estado })).ToList();
        var items = await CargarItemsAsync(con, rows.Select(r => r.ComboID));
        return rows.Select(r => RowToItem(r, items.Where(i => i.ComboID == r.ComboID)));
    }

    public async Task<IEnumerable<ComboItem>> ListarActivosAsync()
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT c.ComboID, c.Nombre, c.Descripcion, c.Estado, c.ModoPrecio,
                   c.FechaInicio, c.FechaFin, c.PrecioManual, c.PorcentajeDescuento,
                   c.ImagenBase64, c.ImagenContentType,
                   c.FechaCreacion,
                   (SELECT SUM(a.PPublico * ci.Cantidad) * (1 - c.PorcentajeDescuento / 100.0)
                    FROM Marketing.ComboItems ci
                    JOIN Catalogo.Tarjetas a ON a.ArticuloID = ci.ArticuloID
                    WHERE ci.ComboID = c.ComboID) AS PrecioCalculado,
                   c.Origen, c.CodigoVisions, c.ReferenciaVisions, c.ExportadoVisions
            FROM Marketing.Combos c
            WHERE c.Estado = 'Activo'
              AND (c.FechaInicio IS NULL OR c.FechaInicio <= CAST(GETDATE() AS DATE))
              AND (c.FechaFin IS NULL OR c.FechaFin >= CAST(GETDATE() AS DATE))
            ORDER BY c.Nombre";

        var rows = (await con.QueryAsync<ListaRow>(sql)).ToList();
        var items = await CargarItemsAsync(con, rows.Select(r => r.ComboID));
        return rows.Select(r => RowToItem(r, items.Where(i => i.ComboID == r.ComboID)));
    }

    public async Task<ComboDetalle?> ObtenerAsync(int id)
    {
        using var con = _db.CreateConnection();

        var combo = await con.QueryFirstOrDefaultAsync<DetalleRow>(
            "SELECT ComboID, Nombre, Descripcion, ImagenBase64, ImagenContentType, Estado, ModoPrecio, FechaInicio, FechaFin, PrecioManual, PorcentajeDescuento, FechaCreacion, Origen, CodigoVisions, ReferenciaVisions, ExportadoVisions FROM Marketing.Combos WHERE ComboID = @id",
            new { id });
        if (combo is null) return null;

        var items = (await con.QueryAsync<ItemCrudo>(@"
            SELECT ci.ComboItemID, ci.ComboID, ci.ArticuloID, a.Referencia AS SKU, a.Nombre AS NombreArticulo,
                   ci.Cantidad, a.PresentacionCodigo AS Unidad, ci.PrecioUnitarioSnapshot, a.PPublico AS PrecioVenta
            FROM Marketing.ComboItems ci
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = ci.ArticuloID
            WHERE ci.ComboID = @id
            ORDER BY ci.ComboItemID", new { id })).ToList();

        decimal? precioCalculado = items.Count > 0
            ? items.Sum(i => (i.PrecioVenta ?? 0) * i.Cantidad) * (1m - combo.PorcentajeDescuento / 100m)
            : null;

        DateOnly? fi = combo.FechaInicio.HasValue ? DateOnly.FromDateTime(combo.FechaInicio.Value) : null;
        DateOnly? ff = combo.FechaFin.HasValue    ? DateOnly.FromDateTime(combo.FechaFin.Value)    : null;

        return new ComboDetalle(
            combo.ComboID, combo.Nombre, combo.Descripcion,
            combo.ImagenBase64, combo.ImagenContentType,
            combo.Estado, combo.ModoPrecio, fi, ff,
            combo.PrecioManual, combo.PorcentajeDescuento, precioCalculado,
            items.Select(i => new ComboItemLine(i.ComboItemID, i.ArticuloID, i.SKU, i.NombreArticulo, i.Cantidad, i.Unidad, i.PrecioUnitarioSnapshot)).ToList(),
            combo.FechaCreacion,
            combo.Origen, combo.CodigoVisions, combo.ReferenciaVisions, combo.ExportadoVisions
        );
    }

    public async Task<int> CrearAsync(CrearComboRequest r, int usuarioId)
    {
        using var con = _db.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        var id = await con.ExecuteScalarAsync<int>(@"
            INSERT INTO Marketing.Combos (Nombre,Descripcion,ImagenBase64,ImagenContentType,Estado,ModoPrecio,FechaInicio,FechaFin,PrecioManual,PorcentajeDescuento,UsuarioCreaID)
            OUTPUT INSERTED.ComboID
            VALUES (@Nombre,@Descripcion,@ImagenBase64,@ImagenContentType,@Estado,@ModoPrecio,@FechaInicio,@FechaFin,@PrecioManual,@PorcentajeDescuento,@UsuarioID)",
            new { r.Nombre, r.Descripcion, r.ImagenBase64, r.ImagenContentType, r.Estado, r.ModoPrecio,
                  FechaInicio = r.FechaInicio.HasValue ? (DateTime?)r.FechaInicio.Value.ToDateTime(TimeOnly.MinValue) : null,
                  FechaFin    = r.FechaFin.HasValue    ? (DateTime?)r.FechaFin.Value.ToDateTime(TimeOnly.MinValue)    : null,
                  r.PrecioManual, r.PorcentajeDescuento, UsuarioID = usuarioId }, tx);

        await InsertarItemsAsync(con, tx, id, r.Items);
        tx.Commit();
        return id;
    }

    public async Task ActualizarAsync(int id, ActualizarComboRequest r)
    {
        using var con = _db.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        var origen = await con.ExecuteScalarAsync<string?>(
            "SELECT Origen FROM Marketing.Combos WHERE ComboID=@id", new { id }, tx);
        if (origen is null) throw new KeyNotFoundException($"Combo {id} no encontrado.");
        if (origen == "VISIONS") throw new InvalidOperationException("Combos creados en VISIONS solo se editan desde VISIONS.");

        var rows = await con.ExecuteAsync(@"
            UPDATE Marketing.Combos
            SET Nombre=@Nombre, Descripcion=@Descripcion, ImagenBase64=@ImagenBase64, ImagenContentType=@ImagenContentType,
                Estado=@Estado, ModoPrecio=@ModoPrecio, FechaInicio=@FechaInicio, FechaFin=@FechaFin,
                PrecioManual=@PrecioManual, PorcentajeDescuento=@PorcentajeDescuento,
                ExportadoVisions=0
            WHERE ComboID=@ID AND Origen='NEXO'",
            new { r.Nombre, r.Descripcion, r.ImagenBase64, r.ImagenContentType, r.Estado, r.ModoPrecio,
                  FechaInicio = r.FechaInicio.HasValue ? (DateTime?)r.FechaInicio.Value.ToDateTime(TimeOnly.MinValue) : null,
                  FechaFin    = r.FechaFin.HasValue    ? (DateTime?)r.FechaFin.Value.ToDateTime(TimeOnly.MinValue)    : null,
                  r.PrecioManual, r.PorcentajeDescuento, ID = id }, tx);

        if (rows == 0) throw new KeyNotFoundException($"Combo {id} no encontrado.");

        await con.ExecuteAsync("DELETE FROM Marketing.ComboItems WHERE ComboID=@id", new { id }, tx);
        await InsertarItemsAsync(con, tx, id, r.Items);
        tx.Commit();
    }

    public async Task<decimal?> CalcularPrecioAsync(int id)
    {
        using var con = _db.CreateConnection();
        return await con.ExecuteScalarAsync<decimal?>(@"
            SELECT SUM(a.PPublico * ci.Cantidad) * (1 - c.PorcentajeDescuento / 100.0)
            FROM Marketing.ComboItems ci
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = ci.ArticuloID
            JOIN Marketing.Combos c ON c.ComboID = ci.ComboID
            WHERE ci.ComboID = @id", new { id });
    }

    private static async Task InsertarItemsAsync(
        System.Data.IDbConnection con, System.Data.IDbTransaction tx,
        int comboId, List<ComboItemInput> items)
    {
        if (items.Count == 0) return;
        const string sql = @"
            INSERT INTO Marketing.ComboItems (ComboID, ArticuloID, Cantidad, PrecioUnitarioSnapshot)
            SELECT @ComboID, @ArticuloID, @Cantidad,
                   (SELECT PPublico FROM Catalogo.Tarjetas WHERE ArticuloID = @ArticuloID)";
        foreach (var item in items)
            await con.ExecuteAsync(sql, new { ComboID = comboId, item.ArticuloID, item.Cantidad }, tx);
    }

    public async Task<IEnumerable<ComboItem>> ListarParaVisionsAsync(DateTime? desde)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT c.ComboID, c.Nombre, c.Descripcion, c.Estado, c.ModoPrecio,
                   c.FechaInicio, c.FechaFin, c.PrecioManual, c.PorcentajeDescuento,
                   CAST(NULL AS NVARCHAR(MAX)) AS ImagenBase64,
                   c.ImagenContentType, c.FechaCreacion,
                   CAST(NULL AS DECIMAL(18,2)) AS PrecioCalculado,
                   c.Origen, c.CodigoVisions, c.ReferenciaVisions, c.ExportadoVisions
            FROM Marketing.Combos c
            WHERE c.Estado = 'Activo'
              AND c.Origen = 'NEXO'
              AND (c.ExportadoVisions = 0 OR @Desde IS NULL OR c.FechaModificacion >= @Desde)";
        var rows = (await con.QueryAsync<ListaRow>(sql, new { Desde = desde })).ToList();
        var items = await CargarItemsAsync(con, rows.Select(r => r.ComboID));
        return rows.Select(r => RowToItem(r, items.Where(i => i.ComboID == r.ComboID)));
    }

    public async Task MarcarExportadoVisionsAsync(int comboId, int codigoVisions, string referenciaVisions)
    {
        using var con = _db.CreateConnection();
        // UPDATE_TIME excluido del trigger de FechaModificacion; no dispara re-exportacion
        var rows = await con.ExecuteAsync(
            "UPDATE Marketing.Combos SET CodigoVisions=@Codigo, ReferenciaVisions=@Ref, ExportadoVisions=1 WHERE ComboID=@ID AND Origen='NEXO'",
            new { Codigo = codigoVisions, Ref = referenciaVisions, ID = comboId });
        if (rows == 0) throw new KeyNotFoundException($"Combo {comboId} no existe o no es de origen NEXO.");
    }

    public async Task<int> SyncDesdeVisionsAsync(SyncComboDesdeVisionsRequest r)
    {
        using var con = _db.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();

        // Resolver ArticuloIDs antes de cualquier write
        var itemsResueltos = new List<(int ArticuloID, decimal Cantidad)>();
        var refsNoEncontradas = new List<string>();
        foreach (var item in r.Items)
        {
            var artId = await con.ExecuteScalarAsync<int?>(
                "SELECT ArticuloID FROM Catalogo.Tarjetas WHERE Referencia=@Ref",
                new { Ref = item.ReferenciaVisions }, tx);
            if (artId.HasValue)
                itemsResueltos.Add((artId.Value, item.Cantidad));
            else
                refsNoEncontradas.Add(item.ReferenciaVisions);
        }
        if (refsNoEncontradas.Count > 0)
            throw new InvalidOperationException($"Referencias no encontradas en NEXO: {string.Join(", ", refsNoEncontradas)}");
        if (itemsResueltos.Count == 0 && r.Items.Count > 0)
            throw new InvalidOperationException("Ningún artículo del combo encontrado en NEXO.");

        var existingRow = await con.QueryFirstOrDefaultAsync<(int ComboID, DateTime FechaMod, string Origen)?>(
            "SELECT ComboID, FechaModificacion, Origen FROM Marketing.Combos WHERE CodigoVisions=@Codigo",
            new { Codigo = r.CodigoVisions }, tx);

        int comboId;
        if (existingRow.HasValue)
        {
            var (existId, fechaMod, origen) = existingRow.Value;
            // Si NEXO fue modificado más reciente (+ 5s margen) → VISIONS tiene datos stale, no sobreescribir
            if (origen == "NEXO" && fechaMod > r.FechaCambio.AddSeconds(5))
            {
                tx.Rollback();
                return existId;
            }
            await con.ExecuteAsync(@"
                UPDATE Marketing.Combos
                SET Nombre=@Nombre, Descripcion=@Descripcion,
                    PrecioManual=@Precio, Estado=@Estado, ExportadoVisions=1
                WHERE ComboID=@ID",
                new { r.Nombre, r.Descripcion, Precio = r.PrecioManual, r.Estado, ID = existId }, tx);
            await con.ExecuteAsync("DELETE FROM Marketing.ComboItems WHERE ComboID=@ID",
                new { ID = existId }, tx);
            comboId = existId;
        }
        else
        {
            comboId = await con.ExecuteScalarAsync<int>(@"
                INSERT INTO Marketing.Combos
                    (Nombre,Descripcion,Estado,ModoPrecio,PrecioManual,PorcentajeDescuento,
                     Origen,CodigoVisions,ReferenciaVisions,ExportadoVisions,UsuarioCreaID)
                OUTPUT INSERTED.ComboID
                VALUES (@Nombre,@Descripcion,@Estado,'MANUAL',@Precio,0,
                        'VISIONS',@CodigoVisions,@Referencia,1,NULL)",
                new { r.Nombre, r.Descripcion, r.Estado, Precio = r.PrecioManual,
                      r.CodigoVisions, Referencia = r.ReferenciaVisions }, tx);
        }

        foreach (var (artId, cantidad) in itemsResueltos)
        {
            var snapshot = await con.ExecuteScalarAsync<decimal?>(
                "SELECT PPublico FROM Catalogo.Tarjetas WHERE ArticuloID=@A", new { A = artId }, tx);
            await con.ExecuteAsync(
                "INSERT INTO Marketing.ComboItems (ComboID,ArticuloID,Cantidad,PrecioUnitarioSnapshot) VALUES (@C,@A,@Q,@S)",
                new { C = comboId, A = artId, Q = cantidad, S = snapshot }, tx);
        }

        tx.Commit();
        return comboId;
    }

    public async Task EliminarComboAsync(int id)
    {
        using var con = _db.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();
        await con.ExecuteAsync("DELETE FROM Marketing.ComboItems WHERE ComboID=@id", new { id }, tx);
        var rows = await con.ExecuteAsync(
            "DELETE FROM Marketing.Combos WHERE ComboID=@id AND Origen='NEXO'", new { id }, tx);
        if (rows == 0) throw new KeyNotFoundException($"Combo {id} no encontrado o es de origen VISIONS.");
        tx.Commit();
    }

    public async Task EliminarComboVisionsAsync(int codigoVisions)
    {
        using var con = _db.CreateConnection();
        await con.ExecuteAsync(
            "UPDATE Marketing.Combos SET Estado='Inactivo', ExportadoVisions=0 WHERE CodigoVisions=@Codigo AND Origen='VISIONS'",
            new { Codigo = codigoVisions });
    }
}
