using Dapper;
using NexoApi.Common.Data;

namespace NexoApi.Features.Auditoria;

public record LogAuditoriaItem(
    long LogID, string? EsquemaTabla, string? RegistroID, string Accion,
    int? UsuarioID, string? NombreUsuario, string? IP,
    DateTime Fecha, string? ValoresNuevos
);

public interface IAuditoriaService
{
    Task<IEnumerable<LogAuditoriaItem>> ListarLogAsync(
        DateTime? desde, DateTime? hasta,
        string? accion, string? modulo,
        string? nombreUsuario, int pagina, int tamano);

    Task<int> ContarLogAsync(
        DateTime? desde, DateTime? hasta,
        string? accion, string? modulo,
        string? nombreUsuario);
}

public class AuditoriaService(IDbConnectionFactory db) : IAuditoriaService
{
    public async Task<IEnumerable<LogAuditoriaItem>> ListarLogAsync(
        DateTime? desde, DateTime? hasta,
        string? accion, string? modulo,
        string? nombreUsuario, int pagina, int tamano)
    {
        using var conn = db.CreateConnection();

        const string sql = @"
            SELECT a.LogID, a.EsquemaTabla, a.RegistroID, a.Accion,
                   a.UsuarioID,
                   ISNULL(a.NombreUsuario, u.Nombres + ' ' + u.Apellidos) AS NombreUsuario,
                   a.IP, a.Fecha, a.ValoresNuevos
            FROM Auditoria.LogAuditoria a
            LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = a.UsuarioID AND a.NombreUsuario IS NULL
            WHERE (@Desde   IS NULL OR a.Fecha >= @Desde)
              AND (@Hasta   IS NULL OR a.Fecha <= @Hasta)
              AND (@Accion  IS NULL OR a.Accion = @Accion)
              AND (@Modulo  IS NULL OR a.EsquemaTabla LIKE @ModuloPattern)
              AND (@NombreUsuario IS NULL OR ISNULL(a.NombreUsuario, u.Nombres + ' ' + u.Apellidos) LIKE @NombreUsuarioPattern)
            ORDER BY a.Fecha DESC
            OFFSET @Offset ROWS FETCH NEXT @Tamano ROWS ONLY";

        return await conn.QueryAsync<LogAuditoriaItem>(sql, new
        {
            Desde = desde,
            Hasta = hasta,
            Accion = accion,
            Modulo = modulo,
            ModuloPattern = modulo != null ? $"%{modulo}%" : (string?)null,
            NombreUsuario = nombreUsuario,
            NombreUsuarioPattern = nombreUsuario != null ? $"%{nombreUsuario}%" : (string?)null,
            Offset = (pagina - 1) * tamano,
            Tamano = tamano
        });
    }

    public async Task<int> ContarLogAsync(
        DateTime? desde, DateTime? hasta,
        string? accion, string? modulo,
        string? nombreUsuario)
    {
        using var conn = db.CreateConnection();

        const string sql = @"
            SELECT COUNT(*)
            FROM Auditoria.LogAuditoria a
            LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = a.UsuarioID AND a.NombreUsuario IS NULL
            WHERE (@Desde   IS NULL OR a.Fecha >= @Desde)
              AND (@Hasta   IS NULL OR a.Fecha <= @Hasta)
              AND (@Accion  IS NULL OR a.Accion = @Accion)
              AND (@Modulo  IS NULL OR a.EsquemaTabla LIKE @ModuloPattern)
              AND (@NombreUsuario IS NULL OR ISNULL(a.NombreUsuario, u.Nombres + ' ' + u.Apellidos) LIKE @NombreUsuarioPattern)";

        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            Desde = desde,
            Hasta = hasta,
            Accion = accion,
            Modulo = modulo,
            ModuloPattern = modulo != null ? $"%{modulo}%" : (string?)null,
            NombreUsuario = nombreUsuario,
            NombreUsuarioPattern = nombreUsuario != null ? $"%{nombreUsuario}%" : (string?)null
        });
    }
}
