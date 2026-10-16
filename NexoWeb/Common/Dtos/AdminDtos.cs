namespace NexoWeb.Common.Dtos;

public record LogAuditoriaItem(
    long LogID, string? EsquemaTabla, string? RegistroID, string Accion,
    int? UsuarioID, string? NombreUsuario, string? IP,
    DateTime Fecha, string? ValoresNuevos
);

public record LogAuditoriaPaginado(List<LogAuditoriaItem> Items, int Total, int Pagina, int Tamano);
