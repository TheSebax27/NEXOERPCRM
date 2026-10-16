using NexoWeb.Common.ApiClient;
using NexoWeb.Common.Dtos;

namespace NexoWeb.Common.Auth;

public record PaginaFavorita(string Href, string Etiqueta);

// Estado de preferencias del usuario actual (tema oscuro, vista lista/tarjetas
// por pantalla, etc.), cacheado en memoria por circuito de Blazor Server y
// respaldado en Seguridad.PreferenciasUsuario via la API. Se carga una sola
// vez (CargarAsync es idempotente) y los componentes se suscriben a OnCambio
// para re-renderizar cuando algo cambia (ej. MainLayout al togglear el tema).
public class PreferenciasState
{
    private readonly INexoApiClient _apiClient;
    private Dictionary<string, string> _valores = new();
    private bool _cargado;

    public event Action? OnCambio;

    public PreferenciasState(INexoApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public bool TemaOscuro => ObtenerValor("TemaOscuro") == "true";

    // Vista Lista/Tarjetas: es un unico control global (Settings), no por
    // pantalla -- antes era por pagina (clave "Vista:{pagina}"), se
    // simplifico a pedido del usuario.
    public string VistaListado => ObtenerValor("VistaListado", "lista");

    // Icono de ayuda por pagina -- activado por defecto (util para usuarios
    // nuevos desde el primer login, sin tener que descubrir el toggle antes).
    public bool MostrarAyuda => ObtenerValor("MostrarAyuda", "true") == "true";

    public bool MantenerSesionActiva => ObtenerValor("MantenerSesionActiva") == "true";

    public bool AntiestresActivo => ObtenerValor("AntiestresActivo") == "true";

    // Paginas marcadas como favoritas (menu lateral) -- se guardan como JSON
    // bajo la clave generica "Favoritos". Requiere que Seguridad.PreferenciasUsuario.Valor
    // sea NVARCHAR(MAX) (ampliado agosto 2026 -- antes era nvarchar(50), muy chico para una lista).
    public List<PaginaFavorita> Favoritos
    {
        get
        {
            var json = ObtenerValor("Favoritos", "[]");
            try { return System.Text.Json.JsonSerializer.Deserialize<List<PaginaFavorita>>(json) ?? new(); }
            catch { return new(); }
        }
    }

    public bool EsFavorito(string href) => Favoritos.Any(f => f.Href == href);

    public async Task ToggleFavoritoAsync(string href, string etiqueta)
    {
        var lista = Favoritos;
        var existente = lista.FirstOrDefault(f => f.Href == href);
        if (existente is not null) lista.Remove(existente);
        else lista.Add(new PaginaFavorita(href, etiqueta));

        await EstablecerAsync("Favoritos", System.Text.Json.JsonSerializer.Serialize(lista));
    }

    public async Task CargarAsync()
    {
        if (_cargado) return;

        try
        {
            _valores = await _apiClient.GetAsync<Dictionary<string, string>>("api/preferencias") ?? new();
        }
        catch
        {
            _valores = new();
        }

        _cargado = true;
    }

    // Se llama al cerrar sesion para que el proximo login (posiblemente de
    // otro usuario en el mismo circuito/pestana) no arrastre preferencias
    // ajenas antes de que CargarAsync las vuelva a pedir.
    public void Reiniciar()
    {
        _valores = new();
        _cargado = false;
    }

    public async Task EstablecerTemaOscuroAsync(bool valor)
    {
        await EstablecerAsync("TemaOscuro", valor ? "true" : "false");
    }

    public async Task EstablecerVistaListadoAsync(string vista)
    {
        await EstablecerAsync("VistaListado", vista);
    }

    public async Task EstablecerMostrarAyudaAsync(bool valor)
    {
        await EstablecerAsync("MostrarAyuda", valor ? "true" : "false");
    }

    public async Task EstablecerMantenerSesionActivaAsync(bool valor)
    {
        await EstablecerAsync("MantenerSesionActiva", valor ? "true" : "false");
    }

    public async Task EstablecerAntiestresActivoAsync(bool valor)
    {
        await EstablecerAsync("AntiestresActivo", valor ? "true" : "false");
    }

    public int IntervaloAntiestresSeg
    {
        get
        {
            var raw = ObtenerValor("IntervaloAntiestresSeg");
            return int.TryParse(raw, out var v) && v >= 10 ? v : 300;
        }
    }

    public async Task EstablecerIntervaloAntiestresSeg(int segundos)
    {
        var valor = Math.Max(10, segundos);
        await EstablecerAsync("IntervaloAntiestresSeg", valor.ToString());
    }

    public bool AutoCodigoOP => ObtenerValor("AutoCodigoOP") == "true";
    public string PrefijoOP   => ObtenerValor("PrefijoOP", "OP");
    public async Task EstablecerAutoCodigoOPAsync(bool valor)   => await EstablecerAsync("AutoCodigoOP", valor ? "true" : "false");
    public async Task EstablecerPrefijoOPAsync(string valor)    => await EstablecerAsync("PrefijoOP", valor.Trim().ToUpper());

    public bool AutoNumeroLote => ObtenerValor("AutoNumeroLote") == "true";
    public string PrefijoLote  => ObtenerValor("PrefijoLote", "LOTE");
    public async Task EstablecerAutoNumeroLoteAsync(bool valor)  => await EstablecerAsync("AutoNumeroLote", valor ? "true" : "false");
    public async Task EstablecerPrefijoLoteAsync(string valor)   => await EstablecerAsync("PrefijoLote", valor.Trim().ToUpper());

    public List<string> OrdenGruposNav
    {
        get
        {
            var json = ObtenerValor("OrdenGruposNav", "[]");
            try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
            catch { return new(); }
        }
    }

    public async Task EstablecerOrdenGruposNavAsync(List<string> orden)
    {
        await EstablecerAsync("OrdenGruposNav", System.Text.Json.JsonSerializer.Serialize(orden));
    }

    private async Task EstablecerAsync(string clave, string valor)
    {
        _valores[clave] = valor;
        OnCambio?.Invoke();

        try
        {
            await _apiClient.PutAsync<GuardarPreferenciaRequest, object>(
                $"api/preferencias/{Uri.EscapeDataString(clave)}", new GuardarPreferenciaRequest(valor));
        }
        catch
        {
            // Si falla el guardado remoto, el valor local ya se aplico visualmente;
            // se reintentara sincronizar en la proxima carga de preferencias.
        }
    }

    private string ObtenerValor(string clave, string porDefecto = "") =>
        _valores.TryGetValue(clave, out var valor) ? valor : porDefecto;
}
