using Dapper;
using NexoApi.Common.Data;

namespace NexoApi.Features.Preferencias;

public interface IPreferenciasService
{
    Task<Dictionary<string, string>> ObtenerTodasAsync(int usuarioId);
    Task GuardarAsync(int usuarioId, string clave, string valor);
}

// Preferencias personales del usuario (tema oscuro, vista lista/tarjetas por
// pantalla, etc.) -- a diferencia de la configuracion de catalogos/roles,
// esto es "por usuario" y cualquiera puede cambiarlo para si mismo sin
// importar su rol. Se guarda como pares Clave/Valor genericos en vez de una
// columna por preferencia para no tener que migrar el esquema cada vez que
// se agregue una nueva (ej. "Vista:articulos", "Vista:consulta-stock").
public class PreferenciasService : IPreferenciasService
{
    private readonly IDbConnectionFactory _db;

    public PreferenciasService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, string>> ObtenerTodasAsync(int usuarioId)
    {
        using var connection = _db.CreateConnection();
        var filas = await connection.QueryAsync<(string Clave, string Valor)>(
            "SELECT Clave, Valor FROM Seguridad.PreferenciasUsuario WHERE UsuarioID = @UsuarioId",
            new { UsuarioId = usuarioId });

        return filas.ToDictionary(f => f.Clave, f => f.Valor);
    }

    public async Task GuardarAsync(int usuarioId, string clave, string valor)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            MERGE Seguridad.PreferenciasUsuario AS destino
            USING (SELECT @UsuarioId AS UsuarioID, @Clave AS Clave) AS origen
            ON destino.UsuarioID = origen.UsuarioID AND destino.Clave = origen.Clave
            WHEN MATCHED THEN
                UPDATE SET Valor = @Valor, FechaModificacion = SYSDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (UsuarioID, Clave, Valor) VALUES (@UsuarioId, @Clave, @Valor);";

        await connection.ExecuteAsync(sql, new { UsuarioId = usuarioId, Clave = clave, Valor = valor });
    }
}
