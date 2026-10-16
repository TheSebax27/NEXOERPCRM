namespace NexoApi.Features.Marketing.Dtos;

// ── Campañas ─────────────────────────────────────────────────────────────────

public record CampanaResumen(
    int CampanaID, string Nombre, string? Descripcion, string Asunto,
    string Estado, string? SegmentoTipo, string? SegmentoValor,
    int TotalDestinatarios, int TotalEnviados, int TotalAbiertos,
    int TotalClicks, int TotalDesuscriptos,
    DateTime FechaCreacion, DateTime? FechaEnvio
);

public record CampanaDetalle(
    int CampanaID, string Nombre, string? Descripcion, string Asunto,
    string BloqueJSON, string Estado, string? SegmentoTipo, string? SegmentoValor,
    int TotalDestinatarios, int TotalEnviados, int TotalAbiertos,
    int TotalClicks, int TotalDesuscriptos,
    DateTime FechaCreacion, DateTime? FechaEnvio
);

public record GuardarCampanaRequest(
    string Nombre, string? Descripcion, string Asunto, string BloqueJSON,
    string? SegmentoTipo, string? SegmentoValor
);

public record PreviewRequest(string BloqueJSON, string NombreCliente = "Juan Ejemplo");

// ── Imágenes ──────────────────────────────────────────────────────────────────

public record ImagenItem(int ImagenID, string Nombre, string ContentType, DateTime FechaSubida);
public record SubirImagenRequest(string Nombre, string ContentType, string DatosBase64);

// ── Segmentación ──────────────────────────────────────────────────────────────

public record ContarDestinatariosResponse(int Total);
public record EnviarPruebaRequest(string EmailDestino);

// ── Tracking ─────────────────────────────────────────────────────────────────

public record EnvioDetalleItem(
    long EnvioID, int ClienteID, string NombreCliente, string Email,
    bool Enviado, DateTime? FechaEnvio,
    bool Abierto, DateTime? FechaApertura,
    int Clicks, bool Desuscripto, string? Error
);
