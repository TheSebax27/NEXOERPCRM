namespace NexoApi.Features.Notificaciones.Dtos;

// Severidad: "error" (sin stock), "warning" (bajo stock), "info" (produccion
// en curso, no es un problema, solo informativo).
public record NotificacionItem(string Categoria, string Severidad, string Mensaje, string Detalle, string Link);

public record ResumenNotificaciones(int Total, List<NotificacionItem> Items);
