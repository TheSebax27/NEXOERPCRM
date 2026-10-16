namespace NexoWeb.Common.Dtos;

public record ClienteItem(
    int ClienteID, string ExternalId, string Nombre, string? NIT, string? Contacto,
    string? Telefono, string? Email, string? Direccion, bool Estado,
    string? FuenteContacto, string? TipoCliente,
    int? ResponsableID, string? Responsable, DateTime? ProximoContacto,
    int TotalContactos, DateTime? UltimaInteraccion,
    string? TipoPersona,
    string? PrimerNombre, string? SegundoNombre, string? PrimerApellido, string? SegundoApellido,
    string? Departamento, string? Ciudad,
    string? TipoIdentificacion, string? CodigoDept, string? CodigoMuni,
    int? DigitoVerificacion = null, string? TipoIdentificacionDetalle = null, string? Notas = null
);

public record CrearClienteRequest(
    string Nombre, string? NIT, string? Telefono, string? Email, string? Direccion,
    string? FuenteContacto, string? TipoCliente, int? ResponsableID,
    string? TipoPersona,
    string? PrimerNombre, string? SegundoNombre, string? PrimerApellido, string? SegundoApellido,
    string? Departamento, string? Ciudad,
    string? TipoIdentificacion = null, string? CodigoDept = null, string? CodigoMuni = null,
    int? DigitoVerificacion = null
);

public record ActualizarClienteRequest(
    string Nombre, string? NIT, string? Telefono, string? Email, string? Direccion, bool Estado,
    string? FuenteContacto, string? TipoCliente, int? ResponsableID, DateTime? ProximoContacto,
    string? TipoPersona,
    string? PrimerNombre, string? SegundoNombre, string? PrimerApellido, string? SegundoApellido,
    string? Departamento, string? Ciudad,
    string? TipoIdentificacion = null, string? CodigoDept = null, string? CodigoMuni = null,
    int? DigitoVerificacion = null, string? Notas = null
);

// Catálogo de referencia geográfica/tributaria
public record PaisItem(string Codigo1, string? Codigo2, string? Codigo3, string Nombre);
public record MunicipioItem(string CodigoDept, string? NombreDept, string CodigoMuni, string? NombreMuni);
public record TipoIdentificacionItem(string Codigo, string? Detalle);

public record ClientesPaginadosResponse(List<ClienteItem> Items, int Total);

public record InteraccionItem(long InteraccionID, int ClienteID, string Tipo, string Notas, DateTime Fecha, string? Usuario);
public record CrearInteraccionRequest(int ClienteID, string Tipo, string Notas, DateTime? ProximoContacto);

public record ContactoItem(int ContactoID, int ClienteID, string Nombres, string? Cargo, string? Telefono, string? Email, bool EsPrincipal, bool Estado);
public record CrearContactoRequest(int ClienteID, string Nombres, string? Cargo, string? Telefono, string? Email, bool EsPrincipal);
public record ActualizarContactoRequest(string Nombres, string? Cargo, string? Telefono, string? Email, bool EsPrincipal, bool Estado);

public record EventoHistorialItem(string TipoEvento, DateTime Fecha, string Titulo, string? Detalle);

public record ClienteDocumentoItem(int DocumentoID, int ClienteID, string TipoDocumento, string NombreArchivo, string ContentType, DateTime FechaSubida);
public record SubirDocumentoRequest(string TipoDocumento, string NombreArchivo, string ContentType, string Base64);

public record LeadItem(
    int LeadID, string Nombre, string? Empresa, string? Telefono, string? Email,
    string? FuenteContacto, string Etapa, string? Notas,
    int? ResponsableID, string? Responsable, int? ClienteIDConvertido,
    DateTime FechaCreacion, DateTime? FechaConversion,
    string? NIT, string? Direccion, string? TipoCliente, string? TipoPersona,
    string? TipoIdentificacion,
    string? PrimerNombre, string? SegundoNombre, string? PrimerApellido, string? SegundoApellido,
    string? Departamento, string? Ciudad,
    string? CodigoDept, string? CodigoMuni, int? DigitoVerificacion,
    int? Score = null
);

public record DuplicadoCandidatoItem(
    string Tipo, int ID, string Nombre, string? NIT, string? Empresa, string? Etapa
);

public record CrearLeadRequest(
    string Nombre, string? Empresa, string? Telefono, string? Email,
    string? FuenteContacto, string? Notas, int? ResponsableID,
    string? NIT = null, string? Direccion = null, string? TipoCliente = null,
    string? TipoPersona = null, string? TipoIdentificacion = null,
    string? PrimerNombre = null, string? SegundoNombre = null,
    string? PrimerApellido = null, string? SegundoApellido = null,
    string? Departamento = null, string? Ciudad = null,
    string? CodigoDept = null, string? CodigoMuni = null, int? DigitoVerificacion = null
);
public record ActualizarLeadRequest(
    string Nombre, string? Empresa, string? Telefono, string? Email,
    string? FuenteContacto, string Etapa, string? Notas, int? ResponsableID,
    string? NIT = null, string? Direccion = null, string? TipoCliente = null,
    string? TipoPersona = null, string? TipoIdentificacion = null,
    string? PrimerNombre = null, string? SegundoNombre = null,
    string? PrimerApellido = null, string? SegundoApellido = null,
    string? Departamento = null, string? Ciudad = null,
    string? CodigoDept = null, string? CodigoMuni = null, int? DigitoVerificacion = null
);
public record ConvertirLeadResponse(int ClienteId);

public record ClienteFrioItem(int ClienteID, string ExternalId, string Nombre, string? Responsable, DateTime? UltimaInteraccion, DateTime? ProximoContacto);

public record CambiarEtapaLeadRequest(string Etapa);

public record NotaLeadItem(
    int NotaID, int LeadID, string Texto, string Tipo,
    string? EtapaOrigen, string? EtapaDestino, string? Usuario, DateTime Fecha);
public record CrearNotaLeadRequest(string Texto, string Tipo = "MANUAL", string? EtapaOrigen = null, string? EtapaDestino = null);

// ---------- Pipeline unificado (Leads + Oportunidades en una sola vista) ----------
public record PipelineItem(
    string Tipo, int ID, int? LeadID, int? OportunidadID, int? ClienteID, string? ExternalIdCliente,
    string Nombre, string? Empresa, string? Telefono, string? Email, string Etapa,
    decimal? ValorEstimado, string? ConfianzaCierre,
    int? ResponsableID, string? Responsable, string? Notas,
    DateTime FechaCreacion, DateTime? FechaCierreEsperada, DateTime? FechaCierre,
    int? Score = null
);
public record CrearOportunidadDesdeLeadRequest(
    string? Nombre, decimal ValorEstimado, string ConfianzaCierre,
    DateTime? FechaCierreEsperada, string? Notas
);

// ---------- Oportunidades (embudo de ventas, agosto 2026) ----------
// ConfianzaCierre: OPTIMISTA | NEUTRO | BAJA
public record OportunidadItem(
    int OportunidadID, int? LeadID, string? Lead, int? ClienteID, string? Cliente,
    string Nombre, decimal ValorEstimado, string ConfianzaCierre, string Etapa,
    DateTime? FechaCierreEsperada, int? ResponsableID, string? Responsable, string? Notas,
    DateTime FechaCreacion, DateTime? FechaCierre
);
public record CrearOportunidadRequest(
    int? LeadID, int? ClienteID, string Nombre, decimal ValorEstimado, string ConfianzaCierre,
    DateTime? FechaCierreEsperada, int? ResponsableID, string? Notas
);
public record ActualizarOportunidadRequest(
    string Nombre, decimal ValorEstimado, string ConfianzaCierre, string Etapa,
    DateTime? FechaCierreEsperada, int? ResponsableID, string? Notas
);

// ---------- Actividades CRM (agosto 2026) ----------
public record ActividadItem(
    int ActividadID, string Tipo, string Titulo, string? Notas,
    DateTime? FechaVencimiento, bool Completada, DateTime? FechaCompletada,
    int? OportunidadID, string? Oportunidad,
    int? ClienteID, string? Cliente,
    int? ResponsableID, string? Responsable,
    DateTime FechaCreacion
);
public record CrearActividadRequest(
    string Tipo, string Titulo, string? Notas, DateTime? FechaVencimiento,
    int? OportunidadID, int? ClienteID, int? ResponsableID
);
public record CompletarActividadRequest(bool Completada);

// ---------- Cotizaciones (agosto 2026) ----------
public record LineaCotizacionInput(int? ArticuloID, decimal Cantidad, decimal PrecioUnitario, int? ComboID = null);
public record CotizacionItem(
    int CotizacionID, int ClienteID, string Cliente, int? OportunidadID, string? OportunidadNombre,
    DateTime Fecha, DateTime? ValidoHasta, string Estado, string? Notas, int? FacturaID, decimal Total,
    int? CentroCostoID = null, string? CentroCosto = null
);
public record CrearCotizacionRequest(int ClienteID, int? OportunidadID, DateTime Fecha, DateTime? ValidoHasta, string? Notas, List<LineaCotizacionInput> Lineas, int? CentroCostoID = null);
public record ActualizarEstadoCotizacionRequest(string Estado);
public record CotizacionLineaItem(int LineaID, int CotizacionID, int? ArticuloID, string? SkuArticulo, string NombreArticulo, decimal Cantidad, decimal PrecioUnitario, decimal Subtotal, int? ComboID = null);
public record ConvertirCotizacionResponse(int FacturaId);

// ---------- Métricas del Pipeline ----------
public record PipelineEtapaConteo(string Etapa, string Etiqueta, int Count);
public record PipelineMetricasDto(
    int LeadsActivos,
    List<PipelineEtapaConteo> LeadsPorEtapa,
    decimal ValorTotalOportunidades,
    int OportunidadesActivas,
    double TasaConversion,
    double? DiasPromedioCierre
);

// ---------- Segmentación y crédito ----------
public record ClienteSegmentoItem(int ClienteID, string Segmento);
public record LineaCreditoItem(int ClienteID, decimal CupoCredito, string? Observaciones);
public record ActualizarLineaCreditoRequest(decimal CupoCredito, string? Observaciones);
public record DisponibilidadCreditoItem(decimal CupoCredito, decimal Utilizado, decimal Disponible);

// ---------- Inteligencia de ventas ----------
public record PrediccionCompraItem(
    double? PromedioDias,
    DateTime? UltimaCompra,
    DateTime? ProximaCompraEsperada,
    int TotalCompras);
