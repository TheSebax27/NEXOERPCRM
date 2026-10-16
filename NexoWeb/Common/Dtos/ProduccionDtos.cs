namespace NexoWeb.Common.Dtos;

public record OrdenProduccionResumen(
    int OrdenProduccionID,
    string CodigoOP,
    string Estado,
    string Producto,
    string CentroCosto,
    decimal CantidadProgramada,
    decimal? CantidadProducidaReal,
    DateTime? FechaPlanificada,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    decimal? CostoUnitarioReal,
    string? TipoProduccion = null
);

public record CerrarOrdenProduccionRequest(
    decimal CantidadProducidaReal,
    decimal HorasManoObra,
    decimal HorasCIF,
    string NumeroLotePT,
    DateTime? FechaVencimientoPT
);

// Agregar a Common/Dtos/ProduccionDtos.cs
public record TipoProduccionItem(int TipoProduccionID, string Codigo, string Nombre);

public record RecetaResumen(
    int RecetaID, int ProductoTerminadoID, string ProductoTerminado, string NombreReceta, int Version,
    decimal CantidadRendimientoBase, string UnidadRendimiento, bool Estado
);

public record MaquinariaRecetaInput(int MaquinariaID, decimal? HorasEstimadasPorLote, string? Notas);
public record MaquinariaOrdenInput(int MaquinariaID, decimal? HorasReales, string? Notas);

public record CrearOrdenProduccionRequest(
    string CodigoOP, int TipoProduccionID, int ProductoTerminadoID, int RecetaID,
    decimal CantidadProgramada, int? ClienteID, int CentroCostoDestinoID,
    int BodegaOrigenMPID, int BodegaDestinoPTID, int? CentroTrabajoID,
    DateTime? FechaPlanificada, string? Observaciones,
    List<MaquinariaOrdenInput>? Maquinas = null,
    List<EmpleadoOrdenInput>? Empleados = null
);

// Solo aplica cuando la orden esta en estado Planificada; el codigo de la OP
// no se puede editar (es el identificador de negocio).
public record ActualizarOrdenProduccionRequest(
    int TipoProduccionID, int ProductoTerminadoID, int RecetaID,
    decimal CantidadProgramada, int? ClienteID, int CentroCostoDestinoID,
    int BodegaOrigenMPID, int BodegaDestinoPTID, int? CentroTrabajoID,
    DateTime? FechaPlanificada, string? Observaciones
);

// IDs crudos (no nombres) para precargar el formulario de edicion.
public record OrdenProduccionDetalleEdicion(
    int OrdenProduccionID, string CodigoOP, string Estado,
    int TipoProduccionID, int ProductoTerminadoID, int RecetaID,
    decimal CantidadProgramada, int? ClienteID, int CentroCostoDestinoID,
    int BodegaOrigenMPID, int BodegaDestinoPTID, int? CentroTrabajoID,
    DateTime? FechaPlanificada, string? Observaciones,
    string? ClienteNombre = null, string? ProductoNombre = null, string? ReferenciaProducto = null
);

public record RecetaDetalleItem(
    int RecetaDetalleID,
    int InsumoID,
    string Insumo,
    decimal CantidadRequerida,
    string Unidad,
    decimal PorcentajeMermaEstandar,
    int? CentroTrabajoID,
    int Orden
);

public record DetalleRecetaRequest(
    int InsumoID,
    decimal CantidadRequerida,
    decimal PorcentajeMermaEstandar,
    int? CentroTrabajoID,
    int Orden
);

public record CrearRecetaRequest(
    int ProductoTerminadoID,
    string NombreReceta,
    decimal CantidadRendimientoBase,
    List<DetalleRecetaRequest> Detalle,
    List<MaquinariaRecetaInput>? Maquinas = null
);

public record CrearNuevaVersionRequest(
    string NombreReceta,
    decimal CantidadRendimientoBase,
    List<DetalleRecetaRequest> Detalle,
    List<MaquinariaRecetaInput>? Maquinas = null
);

public record ConsumoOpItem(
    long ConsumoID, int ArticuloID, string Articulo,
    decimal CantidadTeorica, decimal CantidadReal,
    string? MotivoExceso, string? Observacion
);

public record MotivoExcesoItem(int MotivoExcesoID, string Nombre);

public record AjustarConsumoRealRequest(decimal CantidadReal, int? MotivoExcesoID, string? Observacion);

public record StockLineaItem(
    string Articulo, string Unidad,
    decimal CantidadRequerida, decimal StockDisponible);

public record FaltanteMaterialItem(
    int     OrdenProduccionID,
    string  CodigoOP,
    string  Producto,
    string  Estado,
    string  Insumo,
    string  Unidad,
    decimal CantidadRequerida,
    decimal StockDisponible,
    decimal Faltante
);

public record DesviacionConsumoItem(
    int       OrdenProduccionID,
    string    CodigoOP,
    string    Producto,
    DateTime? FechaFin,
    string    Insumo,
    string    Unidad,
    decimal   CantidadTeorica,
    decimal   CantidadReal,
    decimal   Desviacion
);

public record EmpleadoRecetaItem(int EmpleadoID, string Nombres, string Apellidos, string? Cargo, decimal? TarifaHora, decimal? HorasEstimadasPorLote, string? Notas);
public record EmpleadoOrdenItem(int EmpleadoID, string Nombres, string Apellidos, string? Cargo, decimal? TarifaHora, decimal? HorasReales, string? Notas);
public record EmpleadoOrdenInput(int EmpleadoID, decimal? HorasReales, string? Notas);

// ---------- Subproductos de receta y OP ----------

public record SubproductoRecetaItem(
    int SubproductoBOMID, int RecetaID, int SubproductoID, string Subproducto,
    string? Referencia, decimal CantidadPorLoteBase, string? Unidad,
    int? BodegaDestinoID, string? BodegaDestino, bool Activo
);

public record AgregarSubproductoRecetaRequest(
    int SubproductoID, decimal CantidadPorLoteBase, int? BodegaDestinoID
);

public record SubproductoOpItem(
    long SubproductoOPID, int OrdenProduccionID, int SubproductoID,
    string Subproducto, string? Referencia,
    decimal CantidadGenerada, string? Unidad,
    int? LoteID, long? KardexID, DateTime FechaGeneracion
);