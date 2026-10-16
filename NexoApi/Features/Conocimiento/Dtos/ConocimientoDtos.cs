namespace NexoApi.Features.Conocimiento.Dtos;

public record ArticuloItem(
    int ArticuloID, string Titulo, string Contenido, string Categoria,
    string? Tags, int AutorID, string? AutorNombre,
    DateTime FechaCreacion, DateTime FechaActualizacion, bool Activo, int Vistas
);

public record ArticuloResumenItem(
    int ArticuloID, string Titulo, string Categoria, string? Tags,
    string? AutorNombre, DateTime FechaActualizacion, bool Activo, int Vistas
);

public record CrearArticuloRequest(string Titulo, string Contenido, string Categoria, string? Tags);
public record ActualizarArticuloRequest(string Titulo, string Contenido, string Categoria, string? Tags, bool Activo);
