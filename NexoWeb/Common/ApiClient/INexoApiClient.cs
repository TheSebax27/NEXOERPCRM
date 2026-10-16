using NexoWeb.Common.Dtos;

namespace NexoWeb.Common.ApiClient;

public interface INexoApiClient
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);

    // Metodos genericos: cualquier pantalla los usa para hablar con
    // cualquier endpoint de NexoApi, sin que este cliente tenga que
    // conocer cada modulo de negocio uno por uno.
    Task<T?> GetAsync<T>(string ruta);
    Task<T?> GetAsync<T>(string ruta, CancellationToken ct);

    // Para descargas (Excel, PDF): trae los bytes crudos ya autenticados con
    // el JWT -- un <a href> plano no puede mandar ese header.
    Task<byte[]> GetBytesAsync(string ruta);
    Task<byte[]> PostBytesAsync<TRequest>(string ruta, TRequest body);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string ruta, TRequest body);
    Task PostAsync<TRequest>(string ruta, TRequest body);
    Task<TResponse?> PutAsync<TRequest, TResponse>(string ruta, TRequest body);
    Task PutAsync<TRequest>(string ruta, TRequest body);
    Task PostAsync(string ruta);
    Task DeleteAsync(string ruta);
    Task<TResponse?> PatchAsync<TRequest, TResponse>(string ruta, TRequest body);
    Task PatchAsync(string ruta);
    Task<TResponse?> PostMultipartAsync<TResponse>(string ruta, MultipartFormDataContent content);
}