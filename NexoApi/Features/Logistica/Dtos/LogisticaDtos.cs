namespace NexoApi.Features.Logistica.Dtos;

// NumeroGuia se genera en el SELECT (GUIA-000123 a partir del DespachoID),
// no se guarda como columna aparte.
public record DespachoItem(
    int DespachoID, string NumeroGuia, int ClienteID, string Cliente,
    int CentroCostoID, string CentroCosto, string BodegaOrigen,
    DateTime FechaDespacho, string Estado, DateTime? FechaEntrega,
    string? Direccion, string? Observaciones, decimal ValorTotal,
    string? MotivoAnulacion, DateTime? FechaAnulacion, bool DescuentaStock
);

public record DespachoDetalleItem(int ArticuloID, string SkuArticulo, string NombreArticulo, decimal Cantidad, decimal? ValorUnitario);

// ValorUnitario: precio personalizado por linea (descuento, precio especial).
// Si es null se usa el PrecioVenta del catalogo solo para mostrar el ValorTotal.
public record LineaDespachoRequest(int ArticuloID, decimal Cantidad, decimal? ValorUnitario = null);

public record CrearDespachoRequest(
    int ClienteID, int CentroCostoID, int BodegaOrigenID,
    string? Direccion, string? Observaciones, List<LineaDespachoRequest> Lineas,
    bool DescuentaStock = true
);

public record CrearDespachoResponse(int DespachoId);

public record AnularDespachoRequest(string? Motivo);
