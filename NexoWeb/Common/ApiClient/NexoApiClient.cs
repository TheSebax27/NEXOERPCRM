using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NexoWeb.Common.Auth;
using NexoWeb.Common.Dtos;

namespace NexoWeb.Common.ApiClient;

public class NexoApiClient : INexoApiClient
{
    private readonly HttpClient _http;
    private readonly AuthStateService _authState;
    private readonly NavigationManager _navegacion;

    // Case-insensitive para tolerar camelCase de la API y PascalCase de los DTOs
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private record ErrorDto(string? Error);

    public NexoApiClient(HttpClient http, AuthStateService authState, NavigationManager navegacion)
    {
        _http = http;
        _authState = authState;
        _navegacion = navegacion;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var respuesta = await _http.PostAsJsonAsync("api/auth/login", request);

        if (!respuesta.IsSuccessStatusCode)
            return null;

        return await respuesta.Content.ReadFromJsonAsync<LoginResponse>(_jsonOpts);
    }

    public async Task<T?> GetAsync<T>(string ruta)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.GetAsync(ruta);
        await ValidarRespuestaAsync(respuesta);
        return await respuesta.Content.ReadFromJsonAsync<T>(_jsonOpts);
    }

    public async Task<T?> GetAsync<T>(string ruta, CancellationToken ct)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.GetAsync(ruta, ct);
        await ValidarRespuestaAsync(respuesta);
        return await respuesta.Content.ReadFromJsonAsync<T>(_jsonOpts, ct);
    }

    public async Task<byte[]> GetBytesAsync(string ruta)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.GetAsync(ruta);
        await ValidarRespuestaAsync(respuesta);
        return await respuesta.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> PostBytesAsync<TRequest>(string ruta, TRequest body)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PostAsJsonAsync(ruta, body);
        await ValidarRespuestaAsync(respuesta);
        return await respuesta.Content.ReadAsByteArrayAsync();
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string ruta, TRequest body)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PostAsJsonAsync(ruta, body);
        await ValidarRespuestaAsync(respuesta);
        return await LeerContenidoAsync<TResponse>(respuesta);
    }

    public async Task PostAsync<TRequest>(string ruta, TRequest body)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PostAsJsonAsync(ruta, body);
        await ValidarRespuestaAsync(respuesta);
    }

    public async Task PostAsync(string ruta)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PostAsync(ruta, null);
        await ValidarRespuestaAsync(respuesta);
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(string ruta, TRequest body)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PutAsJsonAsync(ruta, body);
        await ValidarRespuestaAsync(respuesta);
        return await LeerContenidoAsync<TResponse>(respuesta);
    }

    public async Task PutAsync<TRequest>(string ruta, TRequest body)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PutAsJsonAsync(ruta, body);
        await ValidarRespuestaAsync(respuesta);
    }

    public async Task DeleteAsync(string ruta)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.DeleteAsync(ruta);
        await ValidarRespuestaAsync(respuesta);
    }

    public async Task<TResponse?> PatchAsync<TRequest, TResponse>(string ruta, TRequest body)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PatchAsJsonAsync(ruta, body);
        await ValidarRespuestaAsync(respuesta);
        return await LeerContenidoAsync<TResponse>(respuesta);
    }

    public async Task PatchAsync(string ruta)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PatchAsync(ruta, null);
        await ValidarRespuestaAsync(respuesta);
    }

    public async Task<TResponse?> PostMultipartAsync<TResponse>(string ruta, MultipartFormDataContent content)
    {
        await AgregarTokenAsync();
        var respuesta = await _http.PostAsync(ruta, content);
        await ValidarRespuestaAsync(respuesta);
        return await LeerContenidoAsync<TResponse>(respuesta);
    }

    // Algunos endpoints (ej. PUT de actualizacion) responden 204/200 sin cuerpo.
    // ReadFromJsonAsync lanza JsonException ante un body vacio, asi que primero
    // verificamos si hay contenido antes de intentar deserializar.
    private static async Task<T?> LeerContenidoAsync<T>(HttpResponseMessage respuesta)
    {
        if (respuesta.StatusCode == HttpStatusCode.NoContent)
            return default;

        var contenido = await respuesta.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(contenido))
            return default;

        return JsonSerializer.Deserialize<T>(contenido, _jsonOpts);
    }

    private async Task AgregarTokenAsync()
    {
        var token = await _authState.ObtenerTokenAsync();
        _http.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(token) ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task ValidarRespuestaAsync(HttpResponseMessage respuesta)
    {
        if (respuesta.IsSuccessStatusCode) return;

        // Sesion expirada: limpiar estado y redirigir al login
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authState.CerrarSesionAsync();
            _navegacion.NavigateTo("/login", forceLoad: true);
            throw new HttpRequestException("La sesion ha expirado. Por favor inicia sesion nuevamente.");
        }

        // Leer el mensaje de negocio que devuelve ExceptionHandlingMiddleware
        string mensaje;
        try
        {
            var error = await respuesta.Content.ReadFromJsonAsync<ErrorDto>(_jsonOpts);
            mensaje = error?.Error ?? respuesta.ReasonPhrase ?? "Error desconocido.";
        }
        catch
        {
            mensaje = respuesta.ReasonPhrase ?? "Error desconocido.";
        }

        throw new HttpRequestException(mensaje, inner: null, statusCode: respuesta.StatusCode);
    }
}
