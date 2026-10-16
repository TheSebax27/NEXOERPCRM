namespace NexoWeb.Common.Dtos;

public record NotificacionItem(string Categoria, string Severidad, string Mensaje, string Detalle, string Link);

public record ResumenNotificaciones(int Total, List<NotificacionItem> Items);

public record ResultadoBusqueda(string Categoria, string Titulo, string Subtitulo, string Link);
