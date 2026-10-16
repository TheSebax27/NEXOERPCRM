namespace NexoWeb.Common.Dtos;

public record ActividadFeedItem(
    string Tipo, string Descripcion, string Entidad,
    DateTime FechaHora, string Link, string Icono, string Color
);

public record SparklineItem(DateTime Fecha, int Valor);

public record SparklinesDashboard(
    List<SparklineItem> Facturas,
    List<SparklineItem> Ordenes,
    List<SparklineItem> Cotizaciones
);
