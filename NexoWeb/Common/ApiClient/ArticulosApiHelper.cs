using NexoWeb.Common.Dtos;

namespace NexoWeb.Common.ApiClient;

public static class ArticulosApiHelper
{
    public static async Task<IEnumerable<ArticuloItem>> BuscarArticulosAsync(
        this INexoApiClient apiClient, string? texto, CancellationToken ct,
        int max = 15, bool soloTerminados = false)
    {
        try
        {
            var url = $"api/catalogo/articulos/buscar?q={Uri.EscapeDataString(texto ?? "")}&max={max}";
            if (soloTerminados) url += "&soloTerminados=true";
            return await apiClient.GetAsync<List<ArticuloItem>>(url, ct) ?? new();
        }
        catch (OperationCanceledException) { return Enumerable.Empty<ArticuloItem>(); }
        catch { return Enumerable.Empty<ArticuloItem>(); }
    }
}
