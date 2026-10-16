using NexoWeb.Common.ApiClient;

namespace NexoWeb.Common.Auth;

// Controla la visibilidad del NavMenu para el usuario actual.
// Administracion siempre ve todo (bypass). El resto carga sus modulos
// permitidos desde la API una vez por circuito y refresca al cambiar de sesion.
public class ModulosVisiblesState
{
    private readonly INexoApiClient _api;
    private HashSet<string>? _visibles;
    private bool _esAdmin;
    private bool _cargado;

    public event Action? OnCambio;
    public bool Cargado => _cargado;
    public bool EsAdmin => _esAdmin;

    public ModulosVisiblesState(INexoApiClient api) => _api = api;

    // Devuelve true si el modulo debe mostrarse en el menu.
    // Mientras no haya cargado, retorna false (fail-closed) para no mostrar modulos no autorizados.
    public bool EsVisible(string codigo)
    {
        if (_esAdmin) return true;
        if (!_cargado || _visibles is null) return false;
        return _visibles.Contains(codigo);
    }

    public async Task CargarAsync(string? rol, int? usuarioId)
    {
        _esAdmin = rol == "Administracion";
        _cargado = false;

        if (_esAdmin)
        {
            _visibles = null;
            _cargado = true;
            OnCambio?.Invoke();
            return;
        }

        if (usuarioId is null)
        {
            _visibles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _cargado = true;
            return;
        }

        try
        {
            var lista = await _api.GetAsync<List<string>>("api/seguridad/modulos/mis-modulos");
            _visibles = new HashSet<string>(lista ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _visibles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        _cargado = true;
        OnCambio?.Invoke();
    }

    public void Limpiar()
    {
        _visibles = null;
        _esAdmin = false;
        _cargado = false;
        OnCambio?.Invoke();
    }
}
