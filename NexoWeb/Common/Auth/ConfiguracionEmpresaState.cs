using NexoWeb.Common.ApiClient;
using NexoWeb.Common.Dtos;

namespace NexoWeb.Common.Auth;

// Nombre y logo de la empresa (Settings > Mi Negocio, solo Administracion
// puede editarlo) -- a diferencia de PreferenciasState, esto NO es por
// usuario: todos ven lo mismo. Se carga una vez por circuito (CargarAsync es
// idempotente) y dispara OnCambio para que el sidebar se actualice sin
// recargar la pagina cuando un Administracion lo cambia.
public class ConfiguracionEmpresaState
{
    private readonly INexoApiClient _apiClient;
    private bool _cargado;
    private DateTime _ultimoIntento = DateTime.MinValue;

    public bool Cargado => _cargado;

    public string NombreEmpresa { get; private set; } = "NEXO ERP";
    public string? NombrePropietario { get; private set; }
    public string? LogoBase64 { get; private set; }
    public string? LogoContentType { get; private set; }
    public bool UsaVisions { get; private set; } = true;
    public bool ManejarVencimientos { get; private set; } = false;
    public int DiasAlertaVencimiento { get; private set; } = 7;
    public string ModoLotes { get; private set; } = "FIFO";
    public string ModoNroDoc { get; private set; } = "Manual";
    public long UltimoNroDocSecuencial { get; private set; } = 0;
    public bool VentaDesdeReceta { get; private set; } = false;
    public bool SistemaMedidas { get; private set; } = false;
    public bool VenderSinStock { get; private set; } = false;

    // Null si no hay logo propio -- el sidebar usa la imagen por defecto (LogoV.png) en ese caso.
    public string? LogoDataUri => LogoBase64 is null ? null : $"data:{LogoContentType};base64,{LogoBase64}";

    public event Action? OnCambio;

    public ConfiguracionEmpresaState(INexoApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task CargarAsync()
    {
        if (_cargado) return;
        if ((DateTime.UtcNow - _ultimoIntento).TotalSeconds < 10) return;

        _ultimoIntento = DateTime.UtcNow;
        try
        {
            var respuesta = await _apiClient.GetAsync<ConfiguracionEmpresaResponse>("api/configuracion/empresa");
            if (respuesta is not null)
            {
                NombreEmpresa = respuesta.NombreEmpresa;
                NombrePropietario = respuesta.NombrePropietario;
                LogoBase64 = respuesta.LogoBase64;
                LogoContentType = respuesta.LogoContentType;
                UsaVisions = respuesta.UsaVisions;
                ManejarVencimientos = respuesta.ManejarVencimientos;
                DiasAlertaVencimiento = respuesta.DiasAlertaVencimiento;
                ModoLotes = respuesta.ModoLotes;
                ModoNroDoc = respuesta.ModoNroDoc;
                UltimoNroDocSecuencial = respuesta.UltimoNroDocSecuencial;
                VentaDesdeReceta = respuesta.VentaDesdeReceta;
                SistemaMedidas = respuesta.SistemaMedidas;
                VenderSinStock = respuesta.VenderSinStock;
                _cargado = true;
            }
        }
        catch
        {
            // No se marca _cargado para permitir reintento si la API no estaba lista aun.
        }
    }

    public async Task ActualizarNombreAsync(string nombreEmpresa, string? nombrePropietario = null)
    {
        await _apiClient.PutAsync<ActualizarNombreEmpresaRequest, object>(
            "api/configuracion/empresa", new ActualizarNombreEmpresaRequest(nombreEmpresa, nombrePropietario));
        NombreEmpresa = nombreEmpresa;
        NombrePropietario = nombrePropietario;
        OnCambio?.Invoke();
    }

    public async Task ActualizarLogoAsync(string base64, string contentType)
    {
        await _apiClient.PutAsync<ActualizarLogoEmpresaRequest, object>(
            "api/configuracion/empresa/logo", new ActualizarLogoEmpresaRequest(base64, contentType));
        LogoBase64 = base64;
        LogoContentType = contentType;
        OnCambio?.Invoke();
    }

    public async Task EliminarLogoAsync()
    {
        await _apiClient.DeleteAsync("api/configuracion/empresa/logo");
        LogoBase64 = null;
        LogoContentType = null;
        OnCambio?.Invoke();
    }

    public async Task ActualizarUsaVisionsAsync(bool usaVisions)
    {
        await _apiClient.PutAsync<ActualizarUsaVisionsRequest, object>(
            "api/configuracion/empresa/visions", new ActualizarUsaVisionsRequest(usaVisions));
        UsaVisions = usaVisions;
        OnCambio?.Invoke();
    }

    public async Task ActualizarConfigInventarioAsync(bool manejarVencimientos, int diasAlerta, string modoLotes)
    {
        await _apiClient.PutAsync(
            "api/configuracion/empresa/inventario",
            new ActualizarConfigInventarioRequest(manejarVencimientos, diasAlerta, modoLotes));
        ManejarVencimientos = manejarVencimientos;
        DiasAlertaVencimiento = diasAlerta;
        ModoLotes = modoLotes;
        OnCambio?.Invoke();
    }

    public async Task ActualizarConfigNroDocAsync(string modoNroDoc, long ultimoSecuencial)
    {
        await _apiClient.PutAsync(
            "api/configuracion/empresa/nrodoc",
            new ActualizarConfigNroDocRequest(modoNroDoc, ultimoSecuencial));
        ModoNroDoc = modoNroDoc;
        UltimoNroDocSecuencial = ultimoSecuencial;
        OnCambio?.Invoke();
    }

    public async Task ActualizarVentaDesdeRecetaAsync(bool ventaDesdeReceta)
    {
        await _apiClient.PutAsync(
            "api/configuracion/empresa/venta-receta",
            new ActualizarVentaDesdeRecetaRequest(ventaDesdeReceta));
        VentaDesdeReceta = ventaDesdeReceta;
        OnCambio?.Invoke();
    }

    public async Task ActualizarSistemaMedidasAsync(bool sistemaMedidas)
    {
        await _apiClient.PutAsync(
            "api/configuracion/empresa/sistema-medidas",
            new ActualizarSistemaMedidasRequest(sistemaMedidas));
        SistemaMedidas = sistemaMedidas;
        OnCambio?.Invoke();
    }

    public async Task ActualizarVenderSinStockAsync(bool venderSinStock)
    {
        await _apiClient.PutAsync(
            "api/configuracion/empresa/vender-sin-stock",
            new ActualizarVenderSinStockRequest(venderSinStock));
        VenderSinStock = venderSinStock;
        OnCambio?.Invoke();
    }
}
