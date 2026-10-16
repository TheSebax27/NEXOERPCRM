namespace NexoApi.Features.Reportes.Dtos;

public record ReporteFila(Dictionary<string, object?> Columnas);

public record ReporteRequest(
    string Tipo,
    DateTime? Desde,
    DateTime? Hasta,
    int? Limite,
    string? Filtro = null
);

public record ReporteInfo(string Tipo, string Nombre, string Descripcion, string[] Columnas);
