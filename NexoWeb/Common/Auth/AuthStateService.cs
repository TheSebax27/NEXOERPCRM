using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using NexoWeb.Common.Dtos;

namespace NexoWeb.Common.Auth;

// OJO: esta clase NO debe inyectar INexoApiClient -- NexoApiClient depende de
// AuthStateService (para el token del header Authorization), asi que
// inyectarlo aqui crearia un ciclo de DI. Los metodos que tocan el perfil
// (nombre/foto) solo actualizan el estado local; el llamado a la API lo hace
// el componente de UI, que si puede inyectar INexoApiClient sin problema.
public class AuthStateService
{
    private const string ClaveStorage = "nexo_sesion";

    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly ProtectedLocalStorage _localStorage;

    // true cuando la sesión fue guardada en localStorage (recordarme activado)
    private bool _sesionPersistente;

    public string? Token { get; private set; }
    public int? UsuarioId { get; private set; }
    public string? Nombres { get; private set; }
    public string? Apellidos { get; private set; }
    public string? NombreCompleto { get; private set; }
    public string? Rol { get; private set; }
    public int? CentroCostoId { get; private set; }
    public string? FotoPerfilBase64 { get; private set; }
    public string? FotoPerfilContentType { get; private set; }
    public DateTime? ExpiraEn { get; private set; }
    public bool EstaLogueado => !string.IsNullOrEmpty(Token);

    // Data URI listo para <img src="..."> -- null si el usuario no tiene foto.
    public string? FotoPerfilDataUri =>
        FotoPerfilBase64 is null ? null : $"data:{FotoPerfilContentType};base64,{FotoPerfilBase64}";

    // Cualquier componente puede suscribirse a esto para "enterarse" cuando
    // alguien hace login o logout (por ejemplo, el menu lateral, para
    // mostrarse u ocultarse solo), o cuando cambia el nombre/foto del perfil.
    public event Action? OnChange;

    public AuthStateService(ProtectedSessionStorage sessionStorage, ProtectedLocalStorage localStorage)
    {
        _sessionStorage = sessionStorage;
        _localStorage = localStorage;
    }

    // Se llama al arrancar cada circuito nuevo, para recuperar la sesion
    // si el usuario solo recargo la pagina (F5) o cerro el navegador (recordarme).
    public async Task InicializarAsync()
    {
        try
        {
            // Primero intenta localStorage (recordarme); si no, sessionStorage
            var resultadoLocal = await _localStorage.GetAsync<DatosSesion>(ClaveStorage);
            if (resultadoLocal.Success && resultadoLocal.Value is not null)
            {
                CargarDatos(resultadoLocal.Value);
                _sesionPersistente = true;
                return;
            }

            var resultadoSession = await _sessionStorage.GetAsync<DatosSesion>(ClaveStorage);
            if (resultadoSession.Success && resultadoSession.Value is not null)
            {
                CargarDatos(resultadoSession.Value);
                _sesionPersistente = false;
            }
        }
        catch (InvalidOperationException)
        {
            // Pasa durante el "prerendering" inicial de la pagina (antes de
            // que el circuito termine de conectarse); es normal, se ignora.
        }
    }

    public async Task IniciarSesionAsync(LoginResponse respuesta, bool recordarme = false)
    {
        Token = respuesta.AccessToken;
        UsuarioId = respuesta.UsuarioId;
        Nombres = respuesta.Nombres;
        Apellidos = respuesta.Apellidos;
        NombreCompleto = respuesta.NombreCompleto;
        Rol = respuesta.Rol;
        CentroCostoId = respuesta.CentroCostoId;
        FotoPerfilBase64 = respuesta.FotoPerfilBase64;
        FotoPerfilContentType = respuesta.FotoPerfilContentType;
        ExpiraEn = respuesta.ExpiraEn;
        _sesionPersistente = recordarme;

        await GuardarSesionAsync();

        OnChange?.Invoke();
    }

    public async Task ActualizarTokenAsync(string nuevoToken, DateTime expiraEn)
    {
        Token = nuevoToken;
        ExpiraEn = expiraEn;
        await GuardarSesionAsync();
    }

    public async Task ActualizarTokenYRolAsync(string nuevoToken, DateTime expiraEn, string nuevoRol)
    {
        Token = nuevoToken;
        ExpiraEn = expiraEn;
        Rol = nuevoRol;
        await GuardarSesionAsync();
        OnChange?.Invoke();
    }

    public async Task CerrarSesionAsync()
    {
        Token = null;
        UsuarioId = null;
        Nombres = null;
        Apellidos = null;
        NombreCompleto = null;
        Rol = null;
        CentroCostoId = null;
        FotoPerfilBase64 = null;
        FotoPerfilContentType = null;
        ExpiraEn = null;
        _sesionPersistente = false;

        await _sessionStorage.DeleteAsync(ClaveStorage);
        await _localStorage.DeleteAsync(ClaveStorage);

        OnChange?.Invoke();
    }

    // El componente de UI llama primero a la API (PUT /api/auth/perfil) y,
    // si sale bien, llama esto para reflejar el cambio localmente sin tener
    // que recargar la pagina ni volver a loguearse.
    public async Task ActualizarNombreAsync(string nombres, string apellidos)
    {
        Nombres = nombres;
        Apellidos = apellidos;
        NombreCompleto = $"{nombres} {apellidos}";
        await GuardarSesionAsync();
        OnChange?.Invoke();
    }

    public async Task ActualizarFotoAsync(string base64, string contentType)
    {
        FotoPerfilBase64 = base64;
        FotoPerfilContentType = contentType;
        await GuardarSesionAsync();
        OnChange?.Invoke();
    }

    public async Task EliminarFotoAsync()
    {
        FotoPerfilBase64 = null;
        FotoPerfilContentType = null;
        await GuardarSesionAsync();
        OnChange?.Invoke();
    }

    public Task<string?> ObtenerTokenAsync() => Task.FromResult(Token);

    private void CargarDatos(DatosSesion datos)
    {
        Token = datos.Token;
        UsuarioId = datos.UsuarioId;
        Nombres = datos.Nombres;
        Apellidos = datos.Apellidos;
        NombreCompleto = datos.NombreCompleto;
        Rol = datos.Rol;
        CentroCostoId = datos.CentroCostoId;
        FotoPerfilBase64 = datos.FotoPerfilBase64;
        FotoPerfilContentType = datos.FotoPerfilContentType;
        ExpiraEn = datos.ExpiraEn;
    }

    private ValueTask GuardarSesionAsync()
    {
        var datos = new DatosSesion(Token!, UsuarioId, Nombres!, Apellidos!, NombreCompleto!, Rol!, CentroCostoId, FotoPerfilBase64, FotoPerfilContentType, ExpiraEn);
        return _sesionPersistente
            ? _localStorage.SetAsync(ClaveStorage, datos)
            : _sessionStorage.SetAsync(ClaveStorage, datos);
    }

    private record DatosSesion(
        string Token, int? UsuarioId, string Nombres, string Apellidos, string NombreCompleto, string Rol, int? CentroCostoId,
        string? FotoPerfilBase64, string? FotoPerfilContentType, DateTime? ExpiraEn = null);
}
