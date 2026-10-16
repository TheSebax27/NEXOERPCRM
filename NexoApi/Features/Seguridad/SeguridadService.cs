using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Seguridad.Dtos;

namespace NexoApi.Features.Seguridad;

public interface ISeguridadService
{
    // Modulos
    Task<IEnumerable<string>> ObtenerModulosVisiblesUsuarioAsync(int usuarioId);
    Task<IEnumerable<ModuloItem>> ListarModulosAsync();
    Task<IEnumerable<ModuloVisibilidadItem>> ObtenerVisibilidadRolAsync(int rolId);
    Task ActualizarVisibilidadRolAsync(int rolId, List<ItemVisibilidad> items);
    Task<IEnumerable<ModuloVisibilidadItem>> ObtenerVisibilidadUsuarioAsync(int usuarioId);
    Task ActualizarVisibilidadUsuarioAsync(int usuarioId, List<ItemVisibilidad> items);
    Task LimpiarOverridesUsuarioAsync(int usuarioId);

    // Roles
    Task<IEnumerable<RolDetalleItem>> ListarRolesAsync();
    Task<int> CrearRolAsync(CrearRolRequest request);
    Task ActualizarRolAsync(int rolId, ActualizarRolRequest request);
    Task EliminarRolAsync(int rolId);
}

public class SeguridadService : ISeguridadService
{
    private readonly IDbConnectionFactory _db;

    public SeguridadService(IDbConnectionFactory db) => _db = db;

    // ── Modulos ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<string>> ObtenerModulosVisiblesUsuarioAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();
        // Prioridad: override usuario > config de rol > visible por defecto (sin fila)
        const string sql = @"
            SELECT m.Codigo
            FROM Seguridad.Modulos m
            WHERE ISNULL(
                (SELECT TOP 1 pmu.Visible
                 FROM Seguridad.PermisoModuloUsuario pmu
                 WHERE pmu.ModuloID = m.ModuloID AND pmu.UsuarioID = @UsuarioId),
                ISNULL(
                    (SELECT TOP 1 pmr.Visible
                     FROM Seguridad.PermisoModuloRol pmr
                     JOIN Seguridad.Usuarios u ON u.RolID = pmr.RolID
                     WHERE pmr.ModuloID = m.ModuloID AND u.UsuarioID = @UsuarioId),
                    1
                )
            ) = 1
            ORDER BY m.Orden, m.ModuloID";
        return await conn.QueryAsync<string>(sql, new { UsuarioId = usuarioId });
    }

    public async Task<IEnumerable<ModuloItem>> ListarModulosAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ModuloItem>(
            @"SELECT ModuloID, Codigo, Nombre, Ruta, ModuloPadreID, Icono, Orden, EsGrupo
              FROM Seguridad.Modulos
              ORDER BY Orden, ModuloID");
    }

    public async Task<IEnumerable<ModuloVisibilidadItem>> ObtenerVisibilidadRolAsync(int rolId)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            SELECT m.ModuloID, m.Codigo, m.Nombre, pmr.Visible
            FROM Seguridad.Modulos m
            LEFT JOIN Seguridad.PermisoModuloRol pmr
                   ON pmr.ModuloID = m.ModuloID AND pmr.RolID = @RolId
            ORDER BY m.Orden, m.ModuloID";
        return await conn.QueryAsync<ModuloVisibilidadItem>(sql, new { RolId = rolId });
    }

    public async Task ActualizarVisibilidadRolAsync(int rolId, List<ItemVisibilidad> items)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        foreach (var item in items)
        {
            if (item.Visible is null)
            {
                await conn.ExecuteAsync(
                    "DELETE FROM Seguridad.PermisoModuloRol WHERE RolID=@RolId AND ModuloID=@ModuloID",
                    new { RolId = rolId, item.ModuloID }, tx);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    MERGE Seguridad.PermisoModuloRol AS t
                    USING (VALUES (@RolId,@ModuloID,@Visible)) AS s(RolID,ModuloID,Visible)
                    ON t.RolID=s.RolID AND t.ModuloID=s.ModuloID
                    WHEN MATCHED THEN UPDATE SET Visible=s.Visible
                    WHEN NOT MATCHED THEN INSERT(RolID,ModuloID,Visible) VALUES(s.RolID,s.ModuloID,s.Visible);",
                    new { RolId = rolId, item.ModuloID, item.Visible }, tx);
            }
        }

        tx.Commit();
    }

    public async Task<IEnumerable<ModuloVisibilidadItem>> ObtenerVisibilidadUsuarioAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();
        // Devuelve visibilidad EFECTIVA: override usuario > config rol > defecto visible
        // EsOverride=1 cuando hay una fila explicita en PermisoModuloUsuario
        const string sql = @"
            SELECT m.ModuloID, m.Codigo, m.Nombre,
                CAST(ISNULL(
                    pmu.Visible,
                    ISNULL(
                        (SELECT TOP 1 pmr.Visible
                         FROM Seguridad.PermisoModuloRol pmr
                         JOIN Seguridad.Usuarios u ON u.RolID = pmr.RolID
                         WHERE pmr.ModuloID = m.ModuloID AND u.UsuarioID = @UsuarioId),
                        1
                    )
                ) AS BIT) AS Visible,
                CAST(CASE WHEN pmu.Visible IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS EsOverride
            FROM Seguridad.Modulos m
            LEFT JOIN Seguridad.PermisoModuloUsuario pmu
                   ON pmu.ModuloID = m.ModuloID AND pmu.UsuarioID = @UsuarioId
            ORDER BY m.Orden, m.ModuloID";
        return await conn.QueryAsync<ModuloVisibilidadItem>(sql, new { UsuarioId = usuarioId });
    }

    public async Task ActualizarVisibilidadUsuarioAsync(int usuarioId, List<ItemVisibilidad> items)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        foreach (var item in items)
        {
            if (item.Visible is null)
            {
                await conn.ExecuteAsync(
                    "DELETE FROM Seguridad.PermisoModuloUsuario WHERE UsuarioID=@UsuarioId AND ModuloID=@ModuloID",
                    new { UsuarioId = usuarioId, item.ModuloID }, tx);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    MERGE Seguridad.PermisoModuloUsuario AS t
                    USING (VALUES (@UsuarioId,@ModuloID,@Visible)) AS s(UsuarioID,ModuloID,Visible)
                    ON t.UsuarioID=s.UsuarioID AND t.ModuloID=s.ModuloID
                    WHEN MATCHED THEN UPDATE SET Visible=s.Visible
                    WHEN NOT MATCHED THEN INSERT(UsuarioID,ModuloID,Visible) VALUES(s.UsuarioID,s.ModuloID,s.Visible);",
                    new { UsuarioId = usuarioId, item.ModuloID, item.Visible }, tx);
            }
        }

        tx.Commit();
    }

    public async Task LimpiarOverridesUsuarioAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM Seguridad.PermisoModuloUsuario WHERE UsuarioID = @UsuarioId",
            new { UsuarioId = usuarioId });
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<RolDetalleItem>> ListarRolesAsync()
    {
        using var conn = _db.CreateConnection();

        var roles = (await conn.QueryAsync<RolDetalleItem>(@"
            SELECT r.RolID, r.Nombre, r.Descripcion, r.Estado, r.FechaCreacion,
                   (SELECT COUNT(*) FROM Seguridad.Usuarios u WHERE u.RolID = r.RolID) AS CantidadUsuarios
            FROM Seguridad.Roles r
            ORDER BY r.RolID")).ToList();

        if (roles.Count == 0) return roles;

        var cargos = (await conn.QueryAsync<(int RolID, int CargoID, string Nombre, string? Departamento)>(@"
            SELECT c.RolPredeterminadoID AS RolID, c.CargoID, c.Nombre,
                   d.Nombre AS Departamento
            FROM Rrhh.Cargos c
            LEFT JOIN Rrhh.Departamentos d ON d.DepartamentoID = c.DepartamentoID
            WHERE c.RolPredeterminadoID IS NOT NULL AND c.Estado = 1")).ToList();

        var rolDict = roles.ToDictionary(r => r.RolID);
        foreach (var c in cargos)
        {
            if (rolDict.TryGetValue(c.RolID, out var rol))
                rol.Cargos.Add(new CargoVinculadoItem(c.CargoID, c.Nombre, c.Departamento));
        }

        return roles;
    }

    public async Task<int> CrearRolAsync(CrearRolRequest request)
    {
        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Seguridad.Roles (Nombre, Descripcion, Estado, FechaCreacion)
            OUTPUT INSERTED.RolID
            VALUES (@Nombre, @Descripcion, 1, GETDATE())",
            new { request.Nombre, request.Descripcion });
    }

    public async Task ActualizarRolAsync(int rolId, ActualizarRolRequest request)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Seguridad.Roles SET Nombre=@Nombre, Descripcion=@Descripcion, Estado=@Estado WHERE RolID=@RolId",
            new { request.Nombre, request.Descripcion, request.Estado, RolId = rolId });
    }

    public async Task EliminarRolAsync(int rolId)
    {
        using var conn = _db.CreateConnection();

        var usuarios = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Seguridad.Usuarios WHERE RolID = @RolId", new { RolId = rolId });
        if (usuarios > 0)
            throw new InvalidOperationException("No se puede eliminar un rol con usuarios asignados.");

        conn.Open();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("DELETE FROM Seguridad.PermisoModuloRol WHERE RolID=@RolId", new { RolId = rolId }, tx);
        await conn.ExecuteAsync("UPDATE Rrhh.Cargos SET RolPredeterminadoID=NULL WHERE RolPredeterminadoID=@RolId", new { RolId = rolId }, tx);
        await conn.ExecuteAsync("DELETE FROM Seguridad.Roles WHERE RolID=@RolId", new { RolId = rolId }, tx);
        tx.Commit();
    }
}
