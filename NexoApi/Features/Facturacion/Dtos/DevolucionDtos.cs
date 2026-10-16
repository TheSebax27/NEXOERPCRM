namespace NexoApi.Features.Facturacion.Dtos;

public record DevolucionItem(
    int DevolucionID,
    string TipoDevolucion,
    int? FacturaOrigenID, string? NroDocOrigen,
    int? ClienteID, string? Cliente,
    int? ProveedorID, string? Proveedor,
    int? CentroCostoID, string? CentroCosto,
    DateTime Fecha, string? Motivo, string? NroDoc,
    decimal Total, bool StockRestituido, bool OrigenVisions,
    string? Usuario, DateTime FechaRegistro
);

public record DevolucionLineaItem(
    int LineaID, int DevolucionID,
    int ArticuloID, string SKU, string Articulo,
    decimal Cantidad, decimal CostoUnitario, decimal Subtotal,
    string? Nota
);

public record CrearDevolucionRequest(
    string TipoDevolucion,
    int? FacturaOrigenID,
    int? ClienteID,
    int? ProveedorID,
    int? CentroCostoID,
    DateTime Fecha,
    string? Motivo,
    string? NroDoc,
    List<DevolucionLineaInput> Lineas
);

public record DevolucionLineaInput(
    int ArticuloID,
    decimal Cantidad,
    decimal CostoUnitario,
    string? Nota = null
);

public record DevolucionesResumenMes(
    string Mes, int Cantidad, decimal ValorDevuelto
);

public record DevolucionesAnalytics(
    int TotalDevoluciones,
    decimal ValorTotalDevuelto,
    decimal TasaDevolucionPct,
    List<DevolucionesResumenMes> PorMes,
    List<ArticuloMasDevuelto> TopArticulos
);

public record ArticuloMasDevuelto(
    int ArticuloID, string SKU, string Articulo,
    int VecesDevuelto, decimal CantidadTotal, decimal ValorTotal
);
