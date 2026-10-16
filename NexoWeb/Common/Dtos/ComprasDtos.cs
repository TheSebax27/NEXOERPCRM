namespace NexoWeb.Common.Dtos;

public record DetalleOrdenCompraRequest(int ArticuloID, decimal CantidadSolicitada, decimal CostoUnitario);

public record CrearOrdenCompraRequest(
    string Codigo, int ProveedorID, int BodegaDestinoID, List<DetalleOrdenCompraRequest> Detalle,
    int? CentroCostoID = null
);

public record RecibirLineaOrdenCompraRequest(decimal CantidadRecibida, string NumeroLote, DateTime? FechaVencimiento);

public record OrdenCompraResumen(int OrdenCompraID, string Codigo, string Proveedor, string EstadoOC, DateTime FechaEmision, decimal Total, DateTime? FechaRecepcion, bool ExportadoVisions = false, string? NroDocVisions = null, string? TipDocVisions = null);

public record OrdenCompraDetalleItem(
    int OrdenCompraDetalleID, int ArticuloID, string Articulo,
    decimal CantidadSolicitada, decimal CantidadRecibida, decimal CostoUnitario,
    string? Unidad, decimal? UnidadesPorEmbalaje, DateTime? FechaUltimaRecepcion
);

public record ComparacionPrecioRow(
    int ArticuloID, string SKU, string Articulo,
    int ProveedorID, string Proveedor,
    int TotalPedidos,
    decimal PrecioMin, decimal PrecioMax, decimal PrecioPromedio, decimal UltimoPrecio,
    DateTime UltimaCompra
);

public record EntradaVisionsLineaItem(
    string CodigoVisions,
    string NombreVisions,
    decimal Cantidad,
    decimal Costo
);

public record EntradaVisionsDocItem(
    string TipDoc,
    string NroDoc,
    string NitProveedor,
    int? CentroCostoID,
    DateTime FechaDocumento,
    DateTime? FechaProcesado,
    decimal Total,
    List<EntradaVisionsLineaItem> Lineas
);

public record EntradasVisionsPaginadasResponse(
    List<EntradaVisionsDocItem> Items,
    int Total
);