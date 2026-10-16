namespace NexoApi.Features.Crm.Dtos;

// Clientes -- movido desde Features/Catalogo (agosto 2026), ahora con campos
// de seguimiento comercial (FuenteContacto, TipoCliente) y, agosto 2026 (v2),
// Responsable comercial + Proximo Contacto. El campo "Contacto" (texto suelto)
// se mantiene por compatibilidad pero la UI nueva usa Crm.Contactos (multiples
// contactos por cliente) en su lugar.
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
    int? DigitoVerificacion = null,
    string? TipoIdentificacionDetalle = null,
    string? Notas = null
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

// Catálogo de referencia
public record PaisItem(string Codigo1, string? Codigo2, string? Codigo3, string Nombre);
public record MunicipioItem(string CodigoDept, string? NombreDept, string CodigoMuni, string? NombreMuni);
public record TipoIdentificacionItem(string Codigo, string? Detalle);

// Bitacora de interacciones -- llamadas, correos, reuniones con un cliente.
public record InteraccionItem(
    long InteraccionID, int ClienteID, string Tipo, string Notas, DateTime Fecha, string? Usuario
);

public record CrearInteraccionRequest(int ClienteID, string Tipo, string Notas, DateTime? ProximoContacto);

// ---------- A) Multiples contactos por cliente ----------
public record ContactoItem(int ContactoID, int ClienteID, string Nombres, string? Cargo, string? Telefono, string? Email, bool EsPrincipal, bool Estado);
public record CrearContactoRequest(int ClienteID, string Nombres, string? Cargo, string? Telefono, string? Email, bool EsPrincipal);
public record ActualizarContactoRequest(string Nombres, string? Cargo, string? Telefono, string? Email, bool EsPrincipal, bool Estado);

// ---------- B2) Clientes paginados ----------
public record ClientesPaginadosResponse(List<ClienteItem> Items, int Total);

// ---------- C) Historial unificado ----------
// TipoEvento: INTERACCION / PEDIDO / DESPACHO -- un timeline con todo lo que
// ha pasado con el cliente, cruzando Crm + Produccion + Logistica.
public record EventoHistorialItem(string TipoEvento, DateTime Fecha, string Titulo, string? Detalle);

// ---------- E) Documentos adjuntos ----------
public record ClienteDocumentoItem(int DocumentoID, int ClienteID, string TipoDocumento, string NombreArchivo, string ContentType, DateTime FechaSubida);
public record SubirDocumentoRequest(string TipoDocumento, string NombreArchivo, string ContentType, string Base64);

// ---------- Leads (prospectos, distinto de Clientes) ----------
public record LeadItem(
    int LeadID, string Nombre, string? Empresa, string? Telefono, string? Email,
    string? FuenteContacto, string Etapa, string? Notas,
    int? ResponsableID, string? Responsable, int? ClienteIDConvertido,
    DateTime FechaCreacion, DateTime? FechaConversion,
    // Campos de identificación fiscal / ubicación (migration_leads_v1)
    string? NIT, string? Direccion, string? TipoCliente, string? TipoPersona,
    string? TipoIdentificacion,
    string? PrimerNombre, string? SegundoNombre, string? PrimerApellido, string? SegundoApellido,
    string? Departamento, string? Ciudad,
    string? CodigoDept, string? CodigoMuni, int? DigitoVerificacion,
    int? Score = null
);

public record DuplicadoCandidatoItem(
    string Tipo,
    int ID,
    string Nombre,
    string? NIT,
    string? Empresa,
    string? Etapa
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

// ---------- D) Clientes fríos (BI / alertas internas) ----------
public record ClienteFrioItem(int ClienteID, string ExternalId, string Nombre, string? Responsable, DateTime? UltimaInteraccion, DateTime? ProximoContacto);

public record CambiarEtapaLeadRequest(string Etapa);

// ---------- Notas de Lead ----------
public record NotaLeadItem(
    int NotaID, int LeadID, string Texto, string Tipo,
    string? EtapaOrigen, string? EtapaDestino, string? Usuario, DateTime Fecha);
public record CrearNotaLeadRequest(string Texto, string Tipo = "MANUAL", string? EtapaOrigen = null, string? EtapaDestino = null);

// ---------- Pipeline unificado (Leads + Oportunidades en una sola vista) ----------
// Tipo: "LEAD" para registros de Crm.Leads sin oportunidad vinculada.
//       "OPORTUNIDAD" para registros de Crm.Oportunidades.
// Etapas Lead:        NUEVO | CONTACTADO | CALIFICADO | DESCARTADO
// Etapas Oportunidad: PROSPECCION | PROPUESTA | NEGOCIACION | GANADA | PERDIDA
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
// Origen: un Lead sin convertir aun, o un Cliente ya existente -- al menos
// uno de los dos debe venir informado (CK_Oportunidades_OrigenRequerido en BD).
// ConfianzaCierre: OPTIMISTA | NEUTRO | BAJA — reemplaza el numérico Probabilidad%
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

// Datos para generar el PDF de una cotización
public record CotizacionPdfData(
    int CotizacionID, string Cliente, DateTime Fecha, DateTime? ValidoHasta,
    string Estado, string? Notas, string Empresa, List<CotizacionLineaItem> Lineas
);

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

// ---------- Segmentación automática de clientes ----------
// VIP = top 10% en valor facturado últimos 12 meses
// Frecuente = facturas en al menos 3 de los últimos 6 meses
// Nuevo = registrado en últimos 30 días
// Dormido = sin facturas en últimos 60 días (y tiene historial)
// Activo = el resto
public record ClienteSegmentoItem(int ClienteID, string Segmento);

// ---------- Línea de crédito ----------
public record LineaCreditoItem(int ClienteID, decimal CupoCredito, string? Observaciones);
public record ActualizarLineaCreditoRequest(decimal CupoCredito, string? Observaciones);
public record DisponibilidadCreditoItem(decimal CupoCredito, decimal Utilizado, decimal Disponible);

// ---------- Inteligencia de ventas ----------
public record PrediccionCompraItem(
    double? PromedioDias,
    DateTime? UltimaCompra,
    DateTime? ProximaCompraEsperada,
    int TotalCompras);

// Panel recompra: todos los clientes con prediccion de urgencia
public record PanelRecompraItem(
    int ClienteID, string ExternalId, string Nombre, string? Telefono, string? Email,
    string? Responsable,
    DateTime? UltimaCompra, double? PromedioDias, DateTime? ProximaCompra,
    int TotalCompras,
    // Calculados:
    int? DiasDesdeUltima,    // dias desde ultima compra (null si nunca compro)
    int? DiasRestantes,       // positivo = aun tiempo, negativo = pasado de la fecha
    string Urgencia           // VENCIDO / HOY / PROXIMO / NORMAL / SIN_HISTORIAL
);

// Comisiones
public record ConfigComisionItem(
    int ConfigID, int EmpleadoID, string NombreEmpleado,
    decimal PorcentajeBase, decimal MetaMensual, decimal BonoPorMeta, bool Activo);

public record GuardarConfigComisionRequest(
    int EmpleadoID, decimal PorcentajeBase, decimal MetaMensual, decimal BonoPorMeta);

public record DashboardComisionItem(
    int EmpleadoID, string NombreEmpleado,
    decimal MetaMensual, decimal PorcentajeBase, decimal BonoPorMeta,
    decimal TotalVentas, decimal ComisionBase, decimal Bono, decimal TotalComision,
    int TotalFacturas, double PorcentajeMeta);

public record ResumenComisionesItem(
    int EmpleadoID, string NombreEmpleado,
    decimal MetaMensual, decimal TotalVentas,
    decimal TotalComision, double PorcentajeMeta, int TotalFacturas);

// WhatsApp multi-numero
public record NumeroWhatsAppItem(
    int NumeroWAID, string Descripcion, string Numero,
    int? EmpleadoID, string? NombreEmpleado,
    int? CentroCostoID, string? NombreCentroCosto,
    bool Activo);

public record CrearNumeroWhatsAppRequest(
    string Descripcion, string Numero,
    int? EmpleadoID, int? CentroCostoID);

// Historial proveedor
public record EventoHistorialProveedorItem(
    string TipoEvento, DateTime Fecha, string Titulo, string? Detalle);
