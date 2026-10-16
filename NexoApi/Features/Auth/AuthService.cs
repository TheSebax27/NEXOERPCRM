using Dapper;
using NexoApi.Common.Data;
using NexoApi.Common.Security;
using NexoApi.Features.Auth.Dtos;

namespace NexoApi.Features.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<int> RegistrarPrimerAdminAsync(RegistrarUsuarioRequest request);

    // Gestion de usuarios (Administracion)
    Task<IEnumerable<UsuarioItem>> ListarUsuariosAsync();
    Task<int> CrearUsuarioAsync(CrearUsuarioRequest request);
    Task ActualizarUsuarioAsync(int usuarioId, ActualizarUsuarioRequest request);
    Task ResetearPasswordAsync(int usuarioId, ResetearPasswordRequest request);
    Task<IEnumerable<RolItem>> ListarRolesAsync();

    // Mi perfil (el usuario sobre si mismo, sin importar el rol)
    Task<string> ActualizarPerfilAsync(int usuarioId, ActualizarPerfilRequest request);
    Task ActualizarFotoPerfilAsync(int usuarioId, ActualizarFotoPerfilRequest request);
    Task EliminarFotoPerfilAsync(int usuarioId);
    Task CambiarPasswordPerfilAsync(int usuarioId, CambiarPasswordPerfilRequest request);

    Task<RenovarResponse?> RenovarTokenAsync(int usuarioId);
    Task<RenovarResponse?> VolverEmpleadoAsync(int usuarioId);
}

public class AuthService : IAuthService
{
    private readonly IDbConnectionFactory _db;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _config;

    public AuthService(IDbConnectionFactory db, IJwtService jwt, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
    }

    private record UsuarioAuthData(
        int UsuarioID, string Nombres, string Apellidos, string Username,
        byte[] PasswordHash, byte[] Salt, string Rol, int? CentroCostoID, bool Estado,
        byte[]? FotoPerfil, string? FotoPerfilContentType);

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        using var connection = _db.CreateConnection();

        // Se acepta Username O Email como credencial -- es comun que el usuario
        // intente ingresar con su correo (asi lo ve en "Mi perfil"/tarjetas de
        // usuario) sin recordar que el Username puede ser distinto.
        const string sql = @"
            SELECT u.UsuarioID, u.Nombres, u.Apellidos, u.Username,
                   u.PasswordHash, u.Salt, r.Nombre AS Rol, u.CentroCostoID, u.Estado,
                   u.FotoPerfil, u.FotoPerfilContentType
            FROM Seguridad.Usuarios u
            JOIN Seguridad.Roles r ON r.RolID = u.RolID
            WHERE u.Username = @Username OR u.Email = @Username";

        var usuario = await connection.QuerySingleOrDefaultAsync<UsuarioAuthData>(sql, new { request.Username });

        if (usuario is null || !usuario.Estado)
            return null;

        if (!PasswordHasher.VerifyPassword(request.Password, usuario.PasswordHash, usuario.Salt))
            return null;

        var accessToken = _jwt.GenerateAccessToken(usuario.UsuarioID, usuario.Username, usuario.Rol, usuario.CentroCostoID);
        var refreshToken = _jwt.GenerateRefreshToken();
        var expiraEn = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpirationMinutes"]!));
        var refreshExpira = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpirationDays"]!));

        const string insertSesion = @"
            INSERT INTO Seguridad.SesionesUsuario (UsuarioID, Token, RefreshToken, FechaExpiracion, Activa)
            VALUES (@UsuarioID, @Token, @RefreshToken, @FechaExpiracion, 1)";

        await connection.ExecuteAsync(insertSesion, new
        {
            usuario.UsuarioID,
            Token = accessToken,
            RefreshToken = refreshToken,
            FechaExpiracion = refreshExpira
        });

        await connection.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET UltimoAcceso = SYSUTCDATETIME() WHERE UsuarioID = @UsuarioID",
            new { usuario.UsuarioID });

        return new LoginResponse(
            accessToken, refreshToken, expiraEn, usuario.UsuarioID,
            usuario.Nombres, usuario.Apellidos, $"{usuario.Nombres} {usuario.Apellidos}",
            usuario.Rol, usuario.CentroCostoID,
            usuario.FotoPerfil is null ? null : Convert.ToBase64String(usuario.FotoPerfil),
            usuario.FotoPerfilContentType);
    }
    public async Task<int> RegistrarPrimerAdminAsync(RegistrarUsuarioRequest r)
    {
        using var connection = _db.CreateConnection();

        var totalUsuarios = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Seguridad.Usuarios");

        if (totalUsuarios > 0)
            throw new InvalidOperationException(
                "Ya existe al menos un usuario. Este endpoint de arranque queda deshabilitado; los usuarios nuevos se crean autenticado como Administracion.");

        // Resolver el RolID por nombre para no depender del ID generado por migraciones
        var rolId = await connection.ExecuteScalarAsync<int?>(
            "SELECT RolID FROM Seguridad.Roles WHERE Nombre = 'Administracion'");

        if (rolId is null)
            throw new InvalidOperationException(
                "No se encontró el rol 'Administracion' en la base de datos. Verifica que las migraciones se aplicaron correctamente.");

        var (hash, salt) = PasswordHasher.HashPassword(r.Password);

        const string sql = @"
        INSERT INTO Seguridad.Usuarios (Nombres, Apellidos, Email, Username, PasswordHash, Salt, RolID, CentroCostoID)
        OUTPUT INSERTED.UsuarioID
        VALUES (@Nombres, @Apellidos, @Email, @Username, @Hash, @Salt, @RolID, @CentroCostoID)";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            r.Nombres,
            r.Apellidos,
            r.Email,
            r.Username,
            Hash = hash,
            Salt = salt,
            RolID = rolId.Value,
            r.CentroCostoID
        });
    }

    // ================= Gestion de usuarios (Administracion) =================

    public async Task<IEnumerable<UsuarioItem>> ListarUsuariosAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT u.UsuarioID, u.Nombres, u.Apellidos, u.Email, u.Username,
                   u.RolID, r.Nombre AS Rol, u.CentroCostoID, cc.Nombre AS CentroCosto,
                   u.Estado, u.UltimoAcceso, u.EmpleadoID
            FROM Seguridad.Usuarios u
            JOIN Seguridad.Roles r ON r.RolID = u.RolID
            LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = u.CentroCostoID
            ORDER BY u.Nombres, u.Apellidos";

        return await connection.QueryAsync<UsuarioItem>(sql);
    }

    public async Task<int> CrearUsuarioAsync(CrearUsuarioRequest r)
    {
        using var connection = _db.CreateConnection();

        var existe = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Seguridad.Usuarios WHERE Username = @Username OR Email = @Email",
            new { r.Username, r.Email });

        if (existe > 0)
            throw new InvalidOperationException("Ya existe un usuario con ese nombre de usuario o correo.");

        var (hash, salt) = PasswordHasher.HashPassword(r.Password);

        const string sql = @"
            INSERT INTO Seguridad.Usuarios
                (Nombres, Apellidos, Email, Username, PasswordHash, Salt, RolID, CentroCostoID, EmpleadoID, Estado, FechaCreacion)
            OUTPUT INSERTED.UsuarioID
            VALUES
                (@Nombres, @Apellidos, @Email, @Username, @Hash, @Salt, @RolID, @CentroCostoID, @EmpleadoID, 1, SYSUTCDATETIME())";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            r.Nombres,
            r.Apellidos,
            r.Email,
            r.Username,
            Hash = hash,
            Salt = salt,
            r.RolID,
            r.CentroCostoID,
            r.EmpleadoID
        });
    }

    public async Task ActualizarUsuarioAsync(int usuarioId, ActualizarUsuarioRequest r)
    {
        using var connection = _db.CreateConnection();

        var duplicado = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Seguridad.Usuarios WHERE Email = @Email AND UsuarioID <> @UsuarioId",
            new { r.Email, UsuarioId = usuarioId });

        if (duplicado > 0)
            throw new InvalidOperationException($"El correo '{r.Email}' ya está en uso por otro usuario.");

        const string sql = @"
            UPDATE Seguridad.Usuarios
            SET Nombres = @Nombres, Apellidos = @Apellidos, Email = @Email,
                RolID = @RolID, CentroCostoID = @CentroCostoID, Estado = @Estado
            WHERE UsuarioID = @UsuarioId";

        var filas = await connection.ExecuteAsync(sql, new
        {
            UsuarioId = usuarioId,
            r.Nombres,
            r.Apellidos,
            r.Email,
            r.RolID,
            r.CentroCostoID,
            r.Estado
        });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el usuario {usuarioId}.");
    }

    public async Task ResetearPasswordAsync(int usuarioId, ResetearPasswordRequest r)
    {
        using var connection = _db.CreateConnection();

        var (hash, salt) = PasswordHasher.HashPassword(r.NuevaPassword);

        var filas = await connection.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET PasswordHash = @Hash, Salt = @Salt WHERE UsuarioID = @UsuarioId",
            new { Hash = hash, Salt = salt, UsuarioId = usuarioId });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el usuario {usuarioId}.");

        // Invalida las sesiones activas del usuario para que el cambio de clave surta efecto de inmediato.
        await connection.ExecuteAsync(
            "UPDATE Seguridad.SesionesUsuario SET Activa = 0 WHERE UsuarioID = @UsuarioId AND Activa = 1",
            new { UsuarioId = usuarioId });
    }

    public async Task CambiarPasswordPerfilAsync(int usuarioId, CambiarPasswordPerfilRequest r)
    {
        using var connection = _db.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<(byte[] Hash, byte[] Salt)>(
            "SELECT PasswordHash AS Hash, Salt FROM Seguridad.Usuarios WHERE UsuarioID = @UsuarioId",
            new { UsuarioId = usuarioId });
        if (row == default) throw new KeyNotFoundException("Usuario no encontrado.");
        if (!PasswordHasher.VerifyPassword(r.PasswordActual, row.Hash, row.Salt))
            throw new UnauthorizedAccessException("La contraseña actual es incorrecta.");
        var (hash, salt) = PasswordHasher.HashPassword(r.NuevaPassword);
        await connection.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET PasswordHash=@Hash, Salt=@Salt WHERE UsuarioID=@UsuarioId",
            new { Hash = hash, Salt = salt, UsuarioId = usuarioId });
        await connection.ExecuteAsync(
            "UPDATE Seguridad.SesionesUsuario SET Activa=0 WHERE UsuarioID=@UsuarioId AND Activa=1",
            new { UsuarioId = usuarioId });
    }

    public async Task<IEnumerable<RolItem>> ListarRolesAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<RolItem>(
            "SELECT RolID, Nombre FROM Seguridad.Roles WHERE Estado = 1 ORDER BY Nombre");
    }

    // ================= Mi perfil (el usuario sobre si mismo) =================
    // A proposito NO permite tocar RolID/CentroCostoID/Estado -- eso sigue
    // siendo exclusivo de "Gestion de usuarios" (Administracion). Aqui solo
    // nombre y foto, sobre el propio UsuarioID que viene del JWT.

    public async Task<string> ActualizarPerfilAsync(int usuarioId, ActualizarPerfilRequest r)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET Nombres = @Nombres, Apellidos = @Apellidos WHERE UsuarioID = @UsuarioId",
            new { UsuarioId = usuarioId, r.Nombres, r.Apellidos });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el usuario {usuarioId}.");

        return $"{r.Nombres} {r.Apellidos}";
    }

    // El UPDATE sobrescribe FotoPerfil directamente -- no hace falta borrar
    // "el archivo anterior" por separado, no es un archivo en disco, es una
    // sola columna varbinary que sencillamente se reemplaza.
    public async Task ActualizarFotoPerfilAsync(int usuarioId, ActualizarFotoPerfilRequest r)
    {
        using var connection = _db.CreateConnection();

        var datos = Convert.FromBase64String(r.Base64);

        var filas = await connection.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET FotoPerfil = @Datos, FotoPerfilContentType = @ContentType WHERE UsuarioID = @UsuarioId",
            new { UsuarioId = usuarioId, Datos = datos, r.ContentType });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el usuario {usuarioId}.");

        await connection.ExecuteAsync(@"
            UPDATE Rrhh.Empleados
            SET Foto = @Datos, FotoContentType = @ContentType
            WHERE EmpleadoID = (SELECT EmpleadoID FROM Seguridad.Usuarios WHERE UsuarioID = @UsuarioId)",
            new { UsuarioId = usuarioId, Datos = datos, r.ContentType });
    }

    public async Task EliminarFotoPerfilAsync(int usuarioId)
    {
        using var connection = _db.CreateConnection();

        var filas = await connection.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET FotoPerfil = NULL, FotoPerfilContentType = NULL WHERE UsuarioID = @UsuarioId",
            new { UsuarioId = usuarioId });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe el usuario {usuarioId}.");

        await connection.ExecuteAsync(@"
            UPDATE Rrhh.Empleados
            SET Foto = NULL, FotoContentType = NULL
            WHERE EmpleadoID = (SELECT EmpleadoID FROM Seguridad.Usuarios WHERE UsuarioID = @UsuarioId)",
            new { UsuarioId = usuarioId });
    }

    private record UsuarioRenewData(string Username, string Rol, int? CentroCostoID);

    public async Task<RenovarResponse?> RenovarTokenAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();

        var usuario = await conn.QuerySingleOrDefaultAsync<UsuarioRenewData>("""
            SELECT u.Username, r.Nombre AS Rol, u.CentroCostoID
            FROM Seguridad.Usuarios u
            JOIN Seguridad.Roles r ON r.RolID = u.RolID
            WHERE u.UsuarioID = @usuarioId AND u.Estado = 1
            """, new { usuarioId });

        if (usuario is null) return null;

        var accessToken = _jwt.GenerateAccessToken(usuarioId, usuario.Username, usuario.Rol, usuario.CentroCostoID);
        var refreshToken = _jwt.GenerateRefreshToken();
        var expiraEn = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpirationMinutes"]!));
        var refreshExpira = DateTime.UtcNow.AddDays(double.Parse(_config["Jwt:RefreshTokenExpirationDays"]!));

        await conn.ExecuteAsync(
            "UPDATE Seguridad.SesionesUsuario SET Activa = 0 WHERE UsuarioID = @usuarioId AND Activa = 1",
            new { usuarioId });

        await conn.ExecuteAsync("""
            INSERT INTO Seguridad.SesionesUsuario (UsuarioID, Token, RefreshToken, FechaExpiracion, Activa)
            VALUES (@usuarioId, @accessToken, @refreshToken, @refreshExpira, 1)
            """, new { usuarioId, accessToken, refreshToken, refreshExpira });

        return new RenovarResponse(accessToken, expiraEn);
    }

    public async Task<RenovarResponse?> VolverEmpleadoAsync(int usuarioId)
    {
        using var conn = _db.CreateConnection();
        var empleadoRolId = await conn.ExecuteScalarAsync<int?>(
            "SELECT RolID FROM Seguridad.Roles WHERE Nombre = 'Empleados'");
        if (empleadoRolId is null)
            throw new InvalidOperationException("Rol 'Empleados' no encontrado en la base de datos.");
        await conn.ExecuteAsync(
            "UPDATE Seguridad.Usuarios SET RolID = @RolID WHERE UsuarioID = @UsuarioID",
            new { RolID = empleadoRolId, UsuarioID = usuarioId });
        return await RenovarTokenAsync(usuarioId);
    }
}