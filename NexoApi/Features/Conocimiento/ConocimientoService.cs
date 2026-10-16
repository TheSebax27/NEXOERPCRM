using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Conocimiento.Dtos;

namespace NexoApi.Features.Conocimiento;

public interface IConocimientoService
{
    Task<IEnumerable<ArticuloResumenItem>> ListarAsync(string? categoria, string? busqueda);
    Task<ArticuloItem?> ObtenerAsync(int id);
    Task<int> CrearAsync(CrearArticuloRequest req, int autorId);
    Task ActualizarAsync(int id, ActualizarArticuloRequest req);
    Task EliminarAsync(int id);
    Task RegistrarVistaAsync(int id);
    Task<IEnumerable<string>> ListarCategoriasAsync();
}

public class ConocimientoService : IConocimientoService
{
    private readonly IDbConnectionFactory _db;
    public ConocimientoService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<ArticuloResumenItem>> ListarAsync(string? categoria, string? busqueda)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT a.ArticuloID, a.Titulo, a.Categoria, a.Tags,
                   ISNULL(u.Nombres + ' ' + u.Apellidos, '') AS AutorNombre,
                   a.FechaActualizacion, a.Activo, a.Vistas
            FROM Conocimiento.Articulos a
            JOIN Seguridad.Usuarios u ON u.UsuarioID = a.AutorID
            WHERE a.Activo = 1
              AND (@Categoria IS NULL OR a.Categoria = @Categoria)
              AND (@Busqueda  IS NULL OR a.Titulo LIKE '%' + @Busqueda + '%'
                                     OR a.Tags    LIKE '%' + @Busqueda + '%')
            ORDER BY a.FechaActualizacion DESC";
        return await con.QueryAsync<ArticuloResumenItem>(sql,
            new { Categoria = categoria, Busqueda = busqueda });
    }

    public async Task<ArticuloItem?> ObtenerAsync(int id)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT a.ArticuloID, a.Titulo, a.Contenido, a.Categoria, a.Tags,
                   a.AutorID, ISNULL(u.Nombres + ' ' + u.Apellidos, '') AS AutorNombre,
                   a.FechaCreacion, a.FechaActualizacion, a.Activo, a.Vistas
            FROM Conocimiento.Articulos a
            JOIN Seguridad.Usuarios u ON u.UsuarioID = a.AutorID
            WHERE a.ArticuloID = @Id";
        return await con.QueryFirstOrDefaultAsync<ArticuloItem>(sql, new { Id = id });
    }

    public async Task<int> CrearAsync(CrearArticuloRequest req, int autorId)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            INSERT INTO Conocimiento.Articulos (Titulo, Contenido, Categoria, Tags, AutorID)
            OUTPUT INSERTED.ArticuloID
            VALUES (@Titulo, @Contenido, @Categoria, @Tags, @AutorID)";
        return await con.ExecuteScalarAsync<int>(sql,
            new { req.Titulo, req.Contenido, req.Categoria, req.Tags, AutorID = autorId });
    }

    public async Task ActualizarAsync(int id, ActualizarArticuloRequest req)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            UPDATE Conocimiento.Articulos
            SET Titulo = @Titulo, Contenido = @Contenido, Categoria = @Categoria,
                Tags = @Tags, Activo = @Activo, FechaActualizacion = GETDATE()
            WHERE ArticuloID = @Id";
        await con.ExecuteAsync(sql, new { req.Titulo, req.Contenido, req.Categoria, req.Tags, req.Activo, Id = id });
    }

    public async Task EliminarAsync(int id)
    {
        using var con = _db.CreateConnection();
        await con.ExecuteAsync(
            "UPDATE Conocimiento.Articulos SET Activo = 0 WHERE ArticuloID = @Id", new { Id = id });
    }

    public async Task RegistrarVistaAsync(int id)
    {
        using var con = _db.CreateConnection();
        await con.ExecuteAsync(
            "UPDATE Conocimiento.Articulos SET Vistas = Vistas + 1 WHERE ArticuloID = @Id", new { Id = id });
    }

    public async Task<IEnumerable<string>> ListarCategoriasAsync()
    {
        using var con = _db.CreateConnection();
        return await con.QueryAsync<string>(
            "SELECT DISTINCT Categoria FROM Conocimiento.Articulos WHERE Activo = 1 ORDER BY Categoria");
    }
}
