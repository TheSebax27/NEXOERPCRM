using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Formularios.Dtos;

namespace NexoApi.Features.Formularios;

public interface IFormulariosService
{
    Task<IEnumerable<FormularioItem>> ListarAsync(int usuarioId);
    Task<FormularioPublicoItem?> ObtenerPublicoAsync(string token);
    Task<FormularioItem?> ObtenerAsync(int id);
    Task<int> CrearAsync(CrearFormularioRequest request, int usuarioId);
    Task ActualizarAsync(int id, ActualizarFormularioRequest request);
    Task EliminarAsync(int id);
    Task GuardarCamposAsync(int formId, GuardarCamposRequest request);
    Task<IEnumerable<CampoItem>> ListarCamposAsync(int formId);
    Task<IEnumerable<RespuestaItem>> ListarRespuestasAsync(int formId);
    Task EnviarRespuestaAsync(EnviarRespuestaRequest request, string? ip);
}

public class FormulariosService : IFormulariosService
{
    private readonly IDbConnectionFactory _db;
    public FormulariosService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<FormularioItem>> ListarAsync(int usuarioId)
    {
        using var con = _db.CreateConnection();
        return await con.QueryAsync<FormularioItem>(@"
            SELECT f.FormularioID, f.Titulo, f.Descripcion, f.Token, f.Activo,
                   f.AceptaRespuestas, f.MensajeExito, f.CreadoPor,
                   ISNULL(u.Nombres + ' ' + u.Apellidos, '') AS CreadoPorNombre,
                   f.FechaCreacion, f.FechaActualizacion,
                   (SELECT COUNT(*) FROM Formularios.Respuestas WHERE FormularioID = f.FormularioID) AS TotalRespuestas
            FROM Formularios.Formularios f
            JOIN Seguridad.Usuarios u ON u.UsuarioID = f.CreadoPor
            ORDER BY f.FechaCreacion DESC");
    }

    public async Task<FormularioPublicoItem?> ObtenerPublicoAsync(string token)
    {
        using var con = _db.CreateConnection();
        var form = await con.QueryFirstOrDefaultAsync<FormularioItem>(@"
            SELECT f.FormularioID, f.Titulo, f.Descripcion, f.Token, f.Activo,
                   f.AceptaRespuestas, f.MensajeExito, f.CreadoPor,
                   '' AS CreadoPorNombre, f.FechaCreacion, f.FechaActualizacion, 0 AS TotalRespuestas
            FROM Formularios.Formularios f
            WHERE f.Token = @Token AND f.Activo = 1", new { Token = token });

        if (form is null) return null;

        var campos = (await ListarCamposAsync(form.FormularioID)).ToList();
        return new FormularioPublicoItem(
            form.FormularioID, form.Titulo, form.Descripcion,
            form.AceptaRespuestas, form.MensajeExito, campos);
    }

    public async Task<FormularioItem?> ObtenerAsync(int id)
    {
        using var con = _db.CreateConnection();
        return await con.QueryFirstOrDefaultAsync<FormularioItem>(@"
            SELECT f.FormularioID, f.Titulo, f.Descripcion, f.Token, f.Activo,
                   f.AceptaRespuestas, f.MensajeExito, f.CreadoPor,
                   ISNULL(u.Nombres + ' ' + u.Apellidos, '') AS CreadoPorNombre,
                   f.FechaCreacion, f.FechaActualizacion,
                   (SELECT COUNT(*) FROM Formularios.Respuestas WHERE FormularioID = f.FormularioID) AS TotalRespuestas
            FROM Formularios.Formularios f
            JOIN Seguridad.Usuarios u ON u.UsuarioID = f.CreadoPor
            WHERE f.FormularioID = @ID", new { ID = id });
    }

    public async Task<int> CrearAsync(CrearFormularioRequest r, int usuarioId)
    {
        using var con = _db.CreateConnection();
        var token = Guid.NewGuid().ToString("N")[..16];
        return await con.ExecuteScalarAsync<int>(@"
            INSERT INTO Formularios.Formularios
                (Titulo, Descripcion, Token, MensajeExito, CreadoPor)
            OUTPUT INSERTED.FormularioID
            VALUES (@Titulo, @Descripcion, @Token, @MensajeExito, @CreadoPor)",
            new { r.Titulo, r.Descripcion, Token = token, r.MensajeExito, CreadoPor = usuarioId });
    }

    public async Task ActualizarAsync(int id, ActualizarFormularioRequest r)
    {
        using var con = _db.CreateConnection();
        await con.ExecuteAsync(@"
            UPDATE Formularios.Formularios
            SET Titulo = @Titulo, Descripcion = @Descripcion, Activo = @Activo,
                AceptaRespuestas = @AceptaRespuestas, MensajeExito = @MensajeExito,
                FechaActualizacion = GETUTCDATE()
            WHERE FormularioID = @ID",
            new { r.Titulo, r.Descripcion, r.Activo, r.AceptaRespuestas, r.MensajeExito, ID = id });
    }

    public async Task EliminarAsync(int id)
    {
        using var con = _db.CreateConnection();
        await con.ExecuteAsync("DELETE FROM Formularios.Formularios WHERE FormularioID = @ID", new { ID = id });
    }

    public async Task GuardarCamposAsync(int formId, GuardarCamposRequest req)
    {
        using var con = _db.CreateConnection();
        con.Open();
        using var tx = con.BeginTransaction();
        await con.ExecuteAsync("DELETE FROM Formularios.Campos WHERE FormularioID = @FormID",
            new { FormID = formId }, tx);

        foreach (var c in req.Campos)
        {
            await con.ExecuteAsync(@"
                INSERT INTO Formularios.Campos
                    (FormularioID, Tipo, Etiqueta, Placeholder, Requerido, Orden, Opciones)
                VALUES (@FormID, @Tipo, @Etiqueta, @Placeholder, @Requerido, @Orden, @Opciones)",
                new { FormID = formId, c.Tipo, c.Etiqueta, c.Placeholder, c.Requerido, c.Orden, c.Opciones }, tx);
        }
        tx.Commit();
    }

    public async Task<IEnumerable<CampoItem>> ListarCamposAsync(int formId)
    {
        using var con = _db.CreateConnection();
        return await con.QueryAsync<CampoItem>(@"
            SELECT CampoID, FormularioID, Tipo, Etiqueta, Placeholder, Requerido, Orden, Opciones
            FROM Formularios.Campos
            WHERE FormularioID = @FormID
            ORDER BY Orden", new { FormID = formId });
    }

    public async Task<IEnumerable<RespuestaItem>> ListarRespuestasAsync(int formId)
    {
        using var con = _db.CreateConnection();
        return await con.QueryAsync<RespuestaItem>(@"
            SELECT RespuestaID, FormularioID, Datos, IPOrigen, FechaRespuesta
            FROM Formularios.Respuestas
            WHERE FormularioID = @FormID
            ORDER BY FechaRespuesta DESC", new { FormID = formId });
    }

    public async Task EnviarRespuestaAsync(EnviarRespuestaRequest r, string? ip)
    {
        using var con = _db.CreateConnection();
        var acepta = await con.ExecuteScalarAsync<bool>(
            "SELECT AceptaRespuestas FROM Formularios.Formularios WHERE FormularioID = @ID AND Activo = 1",
            new { ID = r.FormularioID });
        if (!acepta) throw new InvalidOperationException("El formulario no acepta respuestas.");

        await con.ExecuteAsync(@"
            INSERT INTO Formularios.Respuestas (FormularioID, Datos, IPOrigen)
            VALUES (@FormularioID, @Datos, @IP)",
            new { r.FormularioID, r.Datos, IP = ip });
    }
}
