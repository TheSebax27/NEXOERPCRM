using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Catalogo.Dtos;

namespace NexoApi.Features.Catalogo;

public interface IAdicionalesService
{
    Task<bool> EsAdicionalAsync(int articuloID);
    Task MarcarEsAdicionalAsync(int articuloID, bool esAdicional);
    Task<IEnumerable<ArticuloAdicionalItem>> ListarDisponiblesAsync(int articuloID);
    Task<IEnumerable<ArticuloAdicionalItem>> ListarAsignadosAsync(int articuloID);
    Task AgregarAsync(int articuloID, int adicionalID);
    Task QuitarAsync(int articuloID, int adicionalID);
    Task<AdicionalesSyncResponse> ObtenerDatosSyncAsync();
    Task SincronizarDesdeVisionsAsync(AdicionalesSyncDesdeVisionsRequest request);
}

public class AdicionalesService : IAdicionalesService
{
    private readonly IDbConnectionFactory _db;

    public AdicionalesService(IDbConnectionFactory db) { _db = db; }

    public async Task<bool> EsAdicionalAsync(int articuloID)
    {
        using var conn = _db.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Catalogo.ArticuloEsAdicional WHERE ArticuloID = @ArticuloID",
            new { ArticuloID = articuloID });
        return count > 0;
    }

    public async Task MarcarEsAdicionalAsync(int articuloID, bool esAdicional)
    {
        using var conn = _db.CreateConnection();
        if (esAdicional)
        {
            await conn.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM Catalogo.ArticuloEsAdicional WHERE ArticuloID = @ArticuloID)
                    INSERT INTO Catalogo.ArticuloEsAdicional (ArticuloID) VALUES (@ArticuloID)
                """, new { ArticuloID = articuloID });
        }
        else
        {
            await conn.ExecuteAsync(
                "DELETE FROM Catalogo.ArticuloEsAdicional WHERE ArticuloID = @ArticuloID",
                new { ArticuloID = articuloID });
        }
    }

    public async Task<IEnumerable<ArticuloAdicionalItem>> ListarDisponiblesAsync(int articuloID)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ArticuloAdicionalItem>("""
            SELECT t.ArticuloID AS AdicionalID, t.Referencia, t.Nombre
            FROM Catalogo.ArticuloEsAdicional ea
            INNER JOIN Catalogo.Tarjetas t ON t.ArticuloID = ea.ArticuloID
            WHERE ea.ArticuloID <> @ArticuloID
              AND ea.ArticuloID NOT IN (
                  SELECT aa.AdicionalID
                  FROM Catalogo.ArticuloAdicionales aa
                  WHERE aa.ArticuloID = @ArticuloID
              )
              AND t.Estado = 1
            ORDER BY t.Nombre
            """, new { ArticuloID = articuloID });
    }

    public async Task<IEnumerable<ArticuloAdicionalItem>> ListarAsignadosAsync(int articuloID)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ArticuloAdicionalItem>("""
            SELECT t.ArticuloID AS AdicionalID, t.Referencia, t.Nombre
            FROM Catalogo.ArticuloAdicionales aa
            INNER JOIN Catalogo.Tarjetas t ON t.ArticuloID = aa.AdicionalID
            WHERE aa.ArticuloID = @ArticuloID
            ORDER BY aa.Orden, t.Nombre
            """, new { ArticuloID = articuloID });
    }

    public async Task AgregarAsync(int articuloID, int adicionalID)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("""
            IF NOT EXISTS (
                SELECT 1 FROM Catalogo.ArticuloAdicionales
                WHERE ArticuloID = @ArticuloID AND AdicionalID = @AdicionalID
            )
                INSERT INTO Catalogo.ArticuloAdicionales (ArticuloID, AdicionalID, Orden)
                VALUES (@ArticuloID, @AdicionalID, 0)
            """, new { ArticuloID = articuloID, AdicionalID = adicionalID });
    }

    public async Task QuitarAsync(int articuloID, int adicionalID)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM Catalogo.ArticuloAdicionales WHERE ArticuloID = @ArticuloID AND AdicionalID = @AdicionalID",
            new { ArticuloID = articuloID, AdicionalID = adicionalID });
    }

    public async Task<AdicionalesSyncResponse> ObtenerDatosSyncAsync()
    {
        using var conn = _db.CreateConnection();

        var esAdicional = (await conn.QueryAsync<EsAdicionalSyncItem>("""
            SELECT t.Referencia
            FROM Catalogo.ArticuloEsAdicional ea
            INNER JOIN Catalogo.Tarjetas t ON t.ArticuloID = ea.ArticuloID
            WHERE t.Estado = 1
            ORDER BY t.Referencia
            """)).ToList();

        var adicionales = (await conn.QueryAsync<AdicionalRelacionSyncItem>("""
            SELECT tp.Referencia, ta.Referencia AS RefAdicional, aa.Orden
            FROM Catalogo.ArticuloAdicionales aa
            INNER JOIN Catalogo.Tarjetas tp ON tp.ArticuloID = aa.ArticuloID
            INNER JOIN Catalogo.Tarjetas ta ON ta.ArticuloID = aa.AdicionalID
            WHERE tp.Estado = 1 AND ta.Estado = 1
            ORDER BY tp.Referencia, aa.Orden, ta.Referencia
            """)).ToList();

        return new AdicionalesSyncResponse(esAdicional, adicionales);
    }

    public async Task SincronizarDesdeVisionsAsync(AdicionalesSyncDesdeVisionsRequest request)
    {
        using var conn = _db.CreateConnection();

        // ── EsAdicional: insertar en NEXO los que Visions tiene y NEXO no ──────
        foreach (var item in request.EsAdicional)
        {
            await conn.ExecuteAsync("""
                DECLARE @Aid INT = (SELECT ArticuloID FROM Catalogo.Tarjetas WHERE Referencia = @Referencia AND Estado = 1);
                IF @Aid IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM Catalogo.ArticuloEsAdicional WHERE ArticuloID = @Aid
                )
                    INSERT INTO Catalogo.ArticuloEsAdicional (ArticuloID) VALUES (@Aid);
                """, new { item.Referencia });
        }

        // ── Relaciones: insertar en NEXO las que Visions tiene y NEXO no ───────
        foreach (var rel in request.Adicionales)
        {
            await conn.ExecuteAsync("""
                DECLARE @PrinID INT = (SELECT ArticuloID FROM Catalogo.Tarjetas WHERE Referencia = @Referencia AND Estado = 1);
                DECLARE @AdID  INT = (SELECT ArticuloID FROM Catalogo.Tarjetas WHERE Referencia = @RefAdicional AND Estado = 1);
                IF @PrinID IS NOT NULL AND @AdID IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM Catalogo.ArticuloAdicionales
                        WHERE ArticuloID = @PrinID AND AdicionalID = @AdID
                    )
                    INSERT INTO Catalogo.ArticuloAdicionales (ArticuloID, AdicionalID, Orden)
                    VALUES (@PrinID, @AdID, @Orden);
                """, new { rel.Referencia, rel.RefAdicional, rel.Orden });
        }
    }
}
