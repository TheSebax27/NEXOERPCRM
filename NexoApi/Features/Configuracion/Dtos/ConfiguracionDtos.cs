namespace NexoApi.Features.Configuracion.Dtos;

public record ConfiguracionEmpresaResponse(
    string NombreEmpresa, string? NombrePropietario,
    string? LogoBase64, string? LogoContentType,
    bool UsaVisions,
    bool ManejarVencimientos, int DiasAlertaVencimiento, string ModoLotes,
    string ModoNroDoc, long UltimoNroDocSecuencial,
    bool VentaDesdeReceta,
    bool SistemaMedidas,
    bool VenderSinStock = false);

public record ActualizarNombreEmpresaRequest(string NombreEmpresa, string? NombrePropietario);
public record ActualizarLogoEmpresaRequest(string Base64, string ContentType);
public record ActualizarUsaVisionsRequest(bool UsaVisions);
public record ActualizarConfigInventarioRequest(bool ManejarVencimientos, int DiasAlertaVencimiento, string ModoLotes);
public record ConfigNroDocResponse(string ModoNroDoc, long UltimoNroDocSecuencial);
public record ActualizarConfigNroDocRequest(string ModoNroDoc, long UltimoNroDocSecuencial);
public record ActualizarVentaDesdeRecetaRequest(bool VentaDesdeReceta);
public record ActualizarSistemaMedidasRequest(bool SistemaMedidas);
public record ActualizarVenderSinStockRequest(bool VenderSinStock);
