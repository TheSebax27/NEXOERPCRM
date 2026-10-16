namespace NexoApi.Features.Auth.Dtos;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiraEn,
    int UsuarioId,
    string Nombres,
    string Apellidos,
    string NombreCompleto,
    string Rol,
    int? CentroCostoId,
    string? FotoPerfilBase64,
    string? FotoPerfilContentType
);

public record RegistrarUsuarioRequest(
    string Nombres,
    string Apellidos,
    string Email,
    string Username,
    string Password,
    int? CentroCostoID
);

// ---------- Gestion de usuarios (Administracion) ----------
public record UsuarioItem(
    int UsuarioID, string Nombres, string Apellidos, string Email, string Username,
    int RolID, string Rol, int? CentroCostoID, string? CentroCosto, bool Estado, DateTime? UltimoAcceso,
    int? EmpleadoID
);

public record RolItem(int RolID, string Nombre);

public record CrearUsuarioRequest(
    string Nombres,
    string Apellidos,
    string Email,
    string Username,
    string Password,
    int RolID,
    int? CentroCostoID,
    int? EmpleadoID = null
);

public record ActualizarUsuarioRequest(
    string Nombres,
    string Apellidos,
    string Email,
    int RolID,
    int? CentroCostoID,
    bool Estado
);

public record ResetearPasswordRequest(string NuevaPassword);

// ---------- Mi perfil (cualquier usuario autenticado, sobre si mismo) ----------
public record ActualizarPerfilRequest(string Nombres, string Apellidos);
public record ActualizarFotoPerfilRequest(string Base64, string ContentType);
public record PerfilActualizadoResponse(string NombreCompleto);

public record RenovarResponse(string AccessToken, DateTime ExpiraEn);
public record CambiarPasswordPerfilRequest(string PasswordActual, string NuevaPassword);