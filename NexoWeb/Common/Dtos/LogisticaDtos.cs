namespace NexoWeb.Common.Dtos;

public record DespachoItem(
    int DespachoID, string NumeroGuia, int ClienteID, string Cliente,
    int CentroCostoID, string CentroCosto, string BodegaOrigen,
    DateTime FechaDespacho, string Estado, DateTime? FechaEntrega,
    string? Direccion, string? Observaciones, decimal ValorTotal,
    string? MotivoAnulacion, DateTime? FechaAnulacion, bool DescuentaStock
);

public record DespachoDetalleItem(int ArticuloID, string SkuArticulo, string NombreArticulo, decimal Cantidad, decimal? ValorUnitario);

public record LineaDespachoRequest(int ArticuloID, decimal Cantidad, decimal? ValorUnitario = null);

public record CrearDespachoRequest(
    int ClienteID, int CentroCostoID, int BodegaOrigenID,
    string? Direccion, string? Observaciones, List<LineaDespachoRequest> Lineas,
    bool DescuentaStock = true
);

public record CrearDespachoResponse(int DespachoId);

public record AnularDespachoRequest(string? Motivo);
