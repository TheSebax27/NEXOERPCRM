namespace NexoApi.Features.Catalogo.Dtos;

// ---------- Centros de Costo ----------
public record CrearCentroCostoRequest(string Codigo, string Nombre, string TipoCentro, string? Direccion, string? Telefono);
// TieneVisions/IdentificadorClienteVisions/BodegaVentaVisionsID solo se
// editan en modo edicion (ver ArticuloDialog): al crear el centro de costo
// todavia no existen sus bodegas, asi que no hay nada que seleccionar como
// "bodega de venta" hasta despues.
// PrefijosDocumentoVentaVisions: lista de codigos TIPDOC (Visions) separados
// por coma, ej. "POS,FV" -- solo esos tipos de documento se exportan como
// venta hacia NEXO. Solo tiene sentido si TieneVisions=true.
public record ActualizarCentroCostoRequest(
    string Nombre, string? Direccion, string? Telefono, bool Estado,
    bool TieneVisions, string? IdentificadorClienteVisions, int? BodegaVentaVisionsID,
    string? PrefijosDocumentoVentaVisions
);
public record CentroCostoItem(
    int CentroCostoID, string Codigo, string Nombre, string TipoCentro, string? Direccion, bool Estado,
    bool TieneVisions, string? IdentificadorClienteVisions, int? BodegaVentaVisionsID,
    string? PrefijosDocumentoVentaVisions
);

// ---------- Bodegas ----------
public record CrearBodegaRequest(string Nombre, int CentroCostoID, string TipoBodega, bool EsVirtual);
// CentroCostoID no se puede cambiar una vez creada (una bodega con stock ya
// esta ligada al costeo de ese centro, igual criterio que el resto del catalogo).
public record ActualizarBodegaRequest(string Nombre, string TipoBodega, bool EsVirtual, bool Estado);
public record BodegaItem(int BodegaID, string Nombre, int CentroCostoID, string CentroCosto, string TipoBodega, bool EsVirtual, bool Estado);

// ---------- Articulos / Tarjetas ----------
// UnidadID es opcional: un Servicio no es tangible, no tiene sentido forzarlo
// a una unidad fisica (kg, L, und, etc.).
// Fracciones: cuantas unidades base trae 1 caja/embalaje (ej. una
// Caja de Fuente trae 10 unidades). Es solo informativo/de conversion para
// ayudar a calcular bien la cantidad -- el stock, kardex y recetas SIEMPRE
// se registran en la UnidadID base del articulo, esto no cambia esa logica.
// Fracciona: 'SI' = vende en fracciones (unidades sueltas Y cajas), 'NO' = solo en paquetes completos.
// Solo aplica cuando Fracciones tiene valor (articulo con unidad de caja).
//
// Campos Visions (calca de TARJETA):
//   Referencia    = TARJETA.REFERENCIA (codigo en Visions, para sincronizacion)
//   Costo         = TARJETA.COSTO
//   PPublico/PBodega/PCredito = TARJETA.PPUBLICO/PBODEGA/PCREDITO
//   UPublico/UBodega/UCredito  = TARJETA.UPUBLICO/UBODEGA/UCREDITO
//   MarcaCodigo   = TARJETA.MARCA  -> catalogo.Marcas.Codigo
//   GrupoMenorCodigo = TARJETA.GRUPOMENOR -> catalogo.GruposMenores.Codigo
//   PresentacionCodigo = TARJETA.PRESENTACION -> catalogo.Presentaciones.Codigo
//   IvaSiNo/IvaValor/IvaDescripcion = TARJETA.IVASINO/IVAVALOR/IVADESCRIPCION
//   Iva2/IvaDescripcion2 = adicion NEXO (doble impuesto), no existe en Visions

public record CrearArticuloRequest(
    string Referencia, string Nombre, string? Descripcion, int TipoArticuloID,
    decimal StockMinimo, decimal PuntoReorden, int? DiasVidaUtil,
    string? Fracciona = "SI", decimal? PrecioVentaUnidad = null,
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
    string Nombre, string? Descripcion,
    decimal StockMinimo, decimal PuntoReorden, int? DiasVidaUtil, bool Estado,
    string? Fracciona = "SI", decimal? PrecioVentaUnidad = null,
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

public record StockPorCC(int CentroCostoID, string NombreCC, decimal Stock);

public record ArticuloItem(
    int ArticuloID, string Referencia, string Nombre, string? Descripcion, string TipoArticulo,
    decimal CostoPromedio, decimal StockMinimo, decimal PuntoReorden, bool Estado, decimal Existencias,
    int? DiasVidaUtil, decimal? Fracciones, bool TieneImagen, string? Fracciona, decimal? PrecioVentaUnidad,
    // Campos Visions (Referencia = TARJETA.REFERENCIA, Nombre = TARJETA.DETALLE)
    decimal? Costo,
    decimal? PPublico, decimal? PBodega, decimal? PCredito,
    decimal? UPublico,  decimal? UBodega, decimal? UCredito,
    string? MarcaCodigo, string? MarcaNombre,
    string? GrupoMenorCodigo, string? GrupoMenorNombre,
    string? GrupoMayorCodigo, string? GrupoMayorNombre,
    string? PresentacionCodigo, string? PresentacionNombre,
    decimal? Peso,
    string? IvaSiNo, short? IvaValor, string? IvaDescripcion,
    short? Iva2, string? IvaDescripcion2,
    int TipoArticuloID = 0,
    List<StockPorCC>? StockPorCentros = null
)
{
    // Constructor secundario para Dapper: las 36 columnas SQL sin StockPorCentros
    public ArticuloItem(
        int ArticuloID, string Referencia, string Nombre, string? Descripcion,
        string TipoArticulo, decimal CostoPromedio, decimal StockMinimo, decimal PuntoReorden,
        bool Estado, decimal Existencias, int? DiasVidaUtil, decimal? Fracciones,
        bool TieneImagen, string? Fracciona, decimal? PrecioVentaUnidad,
        decimal? Costo,
        decimal? PPublico, decimal? PBodega, decimal? PCredito,
        decimal? UPublico, decimal? UBodega, decimal? UCredito,
        string? MarcaCodigo, string? MarcaNombre,
        string? GrupoMenorCodigo, string? GrupoMenorNombre,
        string? GrupoMayorCodigo, string? GrupoMayorNombre,
        string? PresentacionCodigo, string? PresentacionNombre,
        decimal? Peso,
        string? IvaSiNo, short? IvaValor, string? IvaDescripcion,
        short? Iva2, string? IvaDescripcion2,
        int TipoArticuloID)
        : this(ArticuloID, Referencia, Nombre, Descripcion, TipoArticulo, CostoPromedio,
               StockMinimo, PuntoReorden, Estado, Existencias, DiasVidaUtil, Fracciones,
               TieneImagen, Fracciona, PrecioVentaUnidad, Costo,
               PPublico, PBodega, PCredito, UPublico, UBodega, UCredito,
               MarcaCodigo, MarcaNombre, GrupoMenorCodigo, GrupoMenorNombre,
               GrupoMayorCodigo, GrupoMayorNombre, PresentacionCodigo, PresentacionNombre,
               Peso, IvaSiNo, IvaValor, IvaDescripcion, Iva2, IvaDescripcion2, TipoArticuloID, null) { }

    // Helpers para uso en dialogs de despacho/factura
    public bool EsFraccionado => Fracciones is > 0;
    public bool SoloEnPaquete => EsFraccionado && Fracciona == "NO";
    // Alias de compatibilidad (Unidad ahora es PresentacionCodigo — UnidadesMedida eliminado)
    public string? Unidad => PresentacionCodigo;
    public decimal PrecioUnidadEfectivo => PPublico ?? 0;
}

// ---------- Imagen de articulo (opcional, una sola por articulo) ----------
public record ActualizarImagenRequest(string Base64, string ContentType);

// Catalogos base para selectores en formularios
public record TipoArticuloItem(int TipoArticuloID, string Codigo, string Nombre);

// ---------- Importación masiva por Excel ----------
public record ImportarExcelResult(int Creados, int Actualizados, int Errores, List<string> Mensajes);

// ---------- Variantes de artículos ----------
// Un artículo padre puede tener N variantes (Talla S/M/L, Color Rojo/Azul, etc.).
// Cada variante es un artículo normal con su propio SKU que sincroniza con Visions
// de forma independiente: ArticuloPadreID solo afecta la UI de NEXO, no Visions.
public record VarianteItem(int ArticuloID, string Referencia, string Nombre, string? NombreVariante, bool Estado, decimal Existencias);
public record CrearVarianteRequest(string? NombreVariante, string? Referencia, string? Nombre);

public record ResumenTipoItem(string Tipo, int Cantidad);
public record ArticulosPaginadosResponse(List<ArticuloItem> Items, int Total, int Pagina, int Tamano, List<ResumenTipoItem>? ResumenTipos = null);
public record UnidadMedidaItem(int UnidadID, string Nombre, string Abreviatura, string Tipo);

// ---------- Adicionales (toppings/extras — se sincronizan con Visions) ----------
// ArticuloEsAdicional: marca que este artículo puede ser adicional de otro.
// ArticuloAdicionales: relación principal → adicional.
public record ArticuloAdicionalItem(int AdicionalID, string Referencia, string Nombre);
public record MarcarEsAdicionalRequest(bool EsAdicional);
public record AgregarAdicionalRequest(int AdicionalID);

// Payload completo para el agente: snapshot de todo lo que debe quedar en Visions.
public record EsAdicionalSyncItem(string Referencia);
public record AdicionalRelacionSyncItem(string Referencia, string RefAdicional, int Orden);
public record AdicionalesSyncResponse(List<EsAdicionalSyncItem> EsAdicional, List<AdicionalRelacionSyncItem> Adicionales);

// Payload que el agente envía desde Visions → NEXO (solo upsert, sin eliminaciones).
public record AdicionalesSyncDesdeVisionsRequest(
    List<EsAdicionalSyncItem> EsAdicional,
    List<AdicionalRelacionSyncItem> Adicionales);

// ---------- Catalogo: Iva (= dbo.IVA en Visions) ----------
// TARJETA no tiene FK a IVA; almacena IVAVALOR e IVADESCRIPCION como copia plana.
// NEXO sigue el mismo patron: IvaValor e IvaDescripcion en Tarjetas son copias planas.
public record IvaItem(int IvaID, int Iva, string? Descripcion, string TipoImpuesto = "IVA");
public record CrearIvaRequest(int Iva, string? Descripcion, string TipoImpuesto = "IVA");
public record ActualizarIvaRequest(int Iva, string? Descripcion, string TipoImpuesto = "IVA");

// ---------- Catalogo: GruposMayores (= GRUPOMAYOR en Visions) ----------
public record GrupoMayorItem(string Codigo, string? Nombre);
public record CrearGrupoMayorRequest(string Codigo, string Nombre);
public record ActualizarGrupoMayorRequest(string Nombre);

// ---------- Catalogo: GruposMenores (= GRUPOMENOR en Visions) ----------
public record GrupoMenorItem(string Codigo, string? Nombre, string GrupoMayor, string? GrupoMayorNombre);
public record CrearGrupoMenorRequest(string Codigo, string Nombre, string GrupoMayor);
public record ActualizarGrupoMenorRequest(string Nombre);

// ---------- Catalogo: Marcas (= MARCA en Visions) ----------
public record MarcaItem(string Codigo, string? Nombre);
public record CrearMarcaRequest(string Codigo, string Nombre);
public record ActualizarMarcaRequest(string Nombre);

// ---------- Catalogo: Presentaciones (= PRESENTACION en Visions) ----------
// Fracciones: unidades que trae esta presentacion (ej. 12 para CAJA12)
// Tipo: NULL/'UNIDADES' = presentacion normal; 'PESO' = vendido por peso (activa campo Peso en articulo)
public record PresentacionItem(string Codigo, string Presentacion, decimal? Fracciones = null, string? Tipo = null);
public record CrearPresentacionRequest(string Codigo, string Presentacion, decimal? Fracciones = null, string? Tipo = null);
public record ActualizarPresentacionRequest(string Presentacion, decimal? Fracciones = null, string? Tipo = null);

// Clientes se movio a Features/Crm (agosto 2026) -- ver Features/Crm/Dtos/CrmDtos.cs.

public record CentroTrabajoItem(
    int CentroTrabajoID, string Nombre, int CentroCostoID, string CentroCosto,
    decimal CostoHoraManoObra, decimal CostoHoraCIF, bool Estado
);

public record CrearCentroTrabajoRequest(
    string Nombre, int CentroCostoID, decimal CostoHoraManoObra, decimal CostoHoraCIF
);

public record ActualizarCentroTrabajoRequest(
    string Nombre, decimal CostoHoraManoObra, decimal CostoHoraCIF, bool Estado
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
    string? Pais = null, string? CodigoPais = null,
    string? TipoIdentificacionDetalle = null
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
