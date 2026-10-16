namespace NexoWeb.Common.Dtos;

public record DetalleTraspasoRequest(int ArticuloID, int? LoteID, decimal Cantidad);

public record CrearTraspasoRequest(int BodegaOrigenID, int BodegaDestinoID, List<DetalleTraspasoRequest> Detalle);

public record TraspasoResumen(
    int TraspasoID, string Codigo, string BodegaOrigen, string BodegaDestino,
    string EstadoTraspaso, DateTime? FechaEnvio, DateTime? FechaRecepcion
);

public record TraspasoDetalleItem(
    int TraspasoDetalleID, int ArticuloID, string Articulo,
    decimal CantidadEnviada, decimal? CantidadRecibida, decimal CostoUnitario
);