
namespace NexoWeb.Common.Dtos;

// Clientes se movio a Common/Dtos/CrmDtos.cs (agosto 2026).

public record CentroTrabajoItem(
    int CentroTrabajoID,
    string Nombre,
    int CentroCostoID,
    string CentroCosto,
    decimal CostoHoraManoObra,
    decimal CostoHoraCIF,
    bool Estado
);

public record CrearCentroTrabajoRequest(
    string Nombre, int CentroCostoID, decimal CostoHoraManoObra, decimal CostoHoraCIF
);

public record ActualizarCentroTrabajoRequest(
    string Nombre, decimal CostoHoraManoObra, decimal CostoHoraCIF, bool Estado
);

public record CentroCostoItem(
    int CentroCostoID,
    string Codigo,
    string Nombre,
    string TipoCentro,
    string? Direccion,
    bool Estado,
    bool TieneVisions,
    string? IdentificadorClienteVisions,
    int? BodegaVentaVisionsID,
    string? PrefijosDocumentoVentaVisions
);

// Agregado para la pantalla de administracion de Centros de Costo (punto #2).
public record CrearCentroCostoRequest(
    string Codigo,
    string Nombre,
    string TipoCentro,
    string? Direccion,
    string? Telefono
);

public record ActualizarCentroCostoRequest(
    string Nombre,
    string? Direccion,
    string? Telefono,
    bool Estado,
    bool TieneVisions,
    string? IdentificadorClienteVisions,
    int? BodegaVentaVisionsID,
    string? PrefijosDocumentoVentaVisions
);

// ---------- Integracion Visions ----------
public record GenerarApiKeyRequest(int CentroCostoID, string Descripcion);
public record GenerarApiKeyResponse(int AgenteSyncID, string ApiKey);

// Agregado: faltaba este DTO y se usaba en Dashboard, Compras, Inventario,
// Traspasos y Produccion (ArticuloID, SKU, Nombre).


// Reemplaza la version reducida anterior: ahora coincide exactamente con el
// BodegaItem que devuelve la API (antes faltaban CentroCostoID, EsVirtual y
// Estado). Se agregan al final para no romper el binding por nombre que ya
// usan Compras, Inventario, Traspasos y Produccion.
public record BodegaItem(
    int BodegaID,
    string Nombre,
    string CentroCosto,
    string TipoBodega,
    int CentroCostoID,
    bool EsVirtual,
    bool Estado
);

// Agregado para la pantalla de administracion de Bodegas (punto #2).
public record CrearBodegaRequest(
    string Nombre,
    int CentroCostoID,
    string TipoBodega,
    bool EsVirtual
);

// CentroCostoID no se puede cambiar una vez creada la bodega.
public record ActualizarBodegaRequest(
    string Nombre,
    string TipoBodega,
    bool EsVirtual,
    bool Estado
);

public record StockPorCC(int CentroCostoID, string NombreCC, decimal Stock);

// ArticuloItem: campos alineados con Visions v4 (Referencia, Fracciones, Fracciona).
public record ArticuloItem(
    int ArticuloID,
    string Referencia,
    string Nombre,
    string? Descripcion,
    string TipoArticulo,
    decimal CostoPromedio,
    decimal StockMinimo,
    decimal PuntoReorden,
    bool Estado,
    decimal Existencias,
    int? DiasVidaUtil,
    decimal? Fracciones,
    bool TieneImagen,
    string? Fracciona,
    decimal? PrecioVentaUnidad,
    // Campos Visions (Referencia = TARJETA.REFERENCIA, Nombre = TARJETA.DETALLE)
    decimal? Costo = null,
    decimal? PPublico = null, decimal? PBodega = null, decimal? PCredito = null,
    decimal? UPublico = null,  decimal? UBodega = null, decimal? UCredito = null,
    string? MarcaCodigo = null, string? MarcaNombre = null,
    string? GrupoMenorCodigo = null, string? GrupoMenorNombre = null,
    string? GrupoMayorCodigo = null, string? GrupoMayorNombre = null,
    string? PresentacionCodigo = null, string? PresentacionNombre = null,
    decimal? Peso = null,
    string? IvaSiNo = null, short? IvaValor = null, string? IvaDescripcion = null,
    short? Iva2 = null, string? IvaDescripcion2 = null,
    int TipoArticuloID = 0,
    List<StockPorCC>? StockPorCentros = null
)
{
    // Helpers para uso en dialogs de despacho/factura
    public bool EsFraccionado => Fracciones is > 0;
    // Alias de compatibilidad (Unidad ahora es PresentacionCodigo — UnidadesMedida eliminado)
    public string? Unidad            => PresentacionCodigo;
    // Alias de compatibilidad con paginas existentes (migrar progresivamente a Referencia/PPublico/Fracciones)
    public string SKU                => Referencia;
    public decimal PrecioVenta       => PPublico ?? 0;
    public decimal? UnidadesPorEmbalaje => Fracciones;
    public string? ModoVentaCaja     => EsFraccionado ? (Fracciona == "NO" ? "CAJA" : "AMBOS") : null;
    public bool EsUnidadCaja  => EsFraccionado;
    public bool SoloEnPaquete => EsFraccionado && Fracciona == "NO";
    public bool SoloEnCajas   => SoloEnPaquete;
    public decimal PrecioUnidadEfectivo => PrecioVentaUnidad ?? (EsFraccionado ? (PPublico ?? 0) / Fracciones!.Value : PPublico ?? 0);
}

// Imagen opcional, una sola por articulo. CrearArticuloResponse solo se usa
// para leer el ArticuloID nuevo y poder subir la imagen justo despues de crear.
public record ActualizarImagenRequest(string Base64, string ContentType);
public record CrearArticuloResponse(int ArticuloId);

// Lo que cierra ArticuloDialog.
public record ArticuloDialogResultado(object Datos, string? ImagenBase64, string? ImagenContentType);

public record CrearArticuloRequest(
    string Referencia,
    string Nombre,
    string? Descripcion,
    int TipoArticuloID,
    decimal StockMinimo,
    decimal PuntoReorden,
    int? DiasVidaUtil,
    string? Fracciona = "SI",
    decimal? PrecioVentaUnidad = null,
    // Campos Visions (Referencia = TARJETA.REFERENCIA, Nombre = TARJETA.DETALLE)
    decimal? Costo = null,
    decimal? PPublico = null, decimal? PBodega = null, decimal? PCredito = null,
    decimal? UPublico = null,  decimal? UBodega = null, decimal? UCredito = null,
    string? MarcaCodigo = null, string? GrupoMenorCodigo = null, string? PresentacionCodigo = null,
    decimal? Peso = null,
    string? IvaSiNo = null, short? IvaValor = null, string? IvaDescripcion = null,
    short? Iva2 = null, string? IvaDescripcion2 = null
);

public record ActualizarArticuloRequest(
    string Nombre,
    string? Descripcion,
    decimal StockMinimo,
    decimal PuntoReorden,
    int? DiasVidaUtil,
    bool Estado,
    string? Fracciona = "SI",
    decimal? PrecioVentaUnidad = null,
    // Campos Visions (Referencia = TARJETA.REFERENCIA, Nombre = TARJETA.DETALLE)
    decimal? Costo = null,
    decimal? PPublico = null, decimal? PBodega = null, decimal? PCredito = null,
    decimal? UPublico = null,  decimal? UBodega = null, decimal? UCredito = null,
    string? MarcaCodigo = null, string? GrupoMenorCodigo = null, string? PresentacionCodigo = null,
    decimal? Peso = null,
    string? IvaSiNo = null, short? IvaValor = null, string? IvaDescripcion = null,
    short? Iva2 = null, string? IvaDescripcion2 = null,
    int? TipoArticuloId = null
);

public record ProveedorItem(
    int ProveedorID, string RazonSocial, string NIT, string? Contacto,
    string? Telefono, string? Email, string? Direccion, bool Estado,
    string? TipoPersona = null,
    string? PrimerNombre = null, string? SegundoNombre = null,
    string? PrimerApellido = null, string? SegundoApellido = null,
    string? TipoIdentificacion = null, int? DigitoVerificacion = null,
    string? Departamento = null, string? Ciudad = null,
    string? CodigoDept = null, string? CodigoMuni = null,
    string? Pais = null, string? CodigoPais = null
);
public record CrearProveedorRequest(
    string RazonSocial, string NIT, string? Contacto, string? Telefono, string? Email, string? Direccion,
    string? TipoPersona = null,
    string? PrimerNombre = null, string? SegundoNombre = null,
    string? PrimerApellido = null, string? SegundoApellido = null,
    string? TipoIdentificacion = null, int? DigitoVerificacion = null,
    string? Departamento = null, string? Ciudad = null,
    string? CodigoDept = null, string? CodigoMuni = null,
    string? Pais = null, string? CodigoPais = null
);
public record ActualizarProveedorRequest(
    string RazonSocial, string NIT, string? Contacto, string? Telefono, string? Email, string? Direccion, bool Estado,
    string? TipoPersona = null,
    string? PrimerNombre = null, string? SegundoNombre = null,
    string? PrimerApellido = null, string? SegundoApellido = null,
    string? TipoIdentificacion = null, int? DigitoVerificacion = null,
    string? Departamento = null, string? Ciudad = null,
    string? CodigoDept = null, string? CodigoMuni = null,
    string? Pais = null, string? CodigoPais = null
);

// Informa si un NIT ya está registrado como cliente y/o proveedor en NEXO
public record DualRolInfo(bool EsCliente, int? ClienteID, string? ClienteExternalId, bool EsProveedor, int? ProveedorID);

// Auditoría de cambios de precio por artículo
public record HistorialPrecioItem(
    int HistorialID, DateTime FechaCambio, string NombreUsuario,
    string Campo, decimal? ValorAnterior, decimal? ValorNuevo
);

public record TipoArticuloItem(int TipoArticuloID, string Codigo, string Nombre);

// ---------- Importación masiva por Excel ----------
public record ImportarExcelResult(int Creados, int Actualizados, int Errores, List<string>? Mensajes);

// ---------- Variantes de artículos ----------
public record VarianteItem(int ArticuloID, string Referencia, string Nombre, string? NombreVariante, bool Estado, decimal Existencias);
public record CrearVarianteRequest(string? NombreVariante, string? Referencia, string? Nombre);

public record ResumenTipoItem(string Tipo, int Cantidad);
public record ArticulosPaginadosResponse(List<ArticuloItem> Items, int Total, int Pagina, int Tamano, List<ResumenTipoItem>? ResumenTipos = null);
public record UnidadMedidaItem(int UnidadID, string Nombre, string Abreviatura, string Tipo);

// ── Adicionales (toppings/extras) ───────────────────────────────────────
public record ArticuloAdicionalItem(int AdicionalID, string Referencia, string Nombre);
public record MarcarEsAdicionalRequest(bool EsAdicional);
public record AgregarAdicionalRequest(int AdicionalID);

// ── Catalogo: Iva (calca de dbo.IVA de Visions) ────────────────────────
// TARJETA no tiene FK a IVA; almacena valores directamente (copia plana).
public record IvaItem(int IvaID, int Iva, string? Descripcion, string TipoImpuesto = "IVA");
public record CrearIvaRequest(int Iva, string? Descripcion, string TipoImpuesto = "IVA");
public record ActualizarIvaRequest(int Iva, string? Descripcion);

// ── Catalogos Visions ────────────────────────────────────────────────────

public record GrupoMayorItem(string Codigo, string? Nombre);
public record CrearGrupoMayorRequest(string Codigo, string Nombre);
public record ActualizarGrupoMayorRequest(string Nombre);

public record GrupoMenorItem(string Codigo, string? Nombre, string GrupoMayor, string? GrupoMayorNombre);
public record CrearGrupoMenorRequest(string Codigo, string Nombre, string GrupoMayor);
public record ActualizarGrupoMenorRequest(string Nombre);

public record MarcaItem(string Codigo, string? Nombre);
public record CrearMarcaRequest(string Codigo, string Nombre);
public record ActualizarMarcaRequest(string Nombre);

public record PresentacionItem(string Codigo, string Presentacion, decimal? Fracciones = null, string? Tipo = null)
{
    public bool EsDePeso => Tipo == "PESO";
}
public record CrearPresentacionRequest(string Codigo, string Presentacion, decimal? Fracciones = null, string? Tipo = null);
public record ActualizarPresentacionRequest(string Presentacion, decimal? Fracciones = null, string? Tipo = null);