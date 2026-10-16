namespace NexoApi.Features.Soporte.Dtos;

public record TicketItem(
    int TicketID, string Titulo, string Descripcion, string Categoria,
    string Prioridad, string Estado,
    int ReportadoPor, string? ReportadoPorNombre,
    int? AsignadoA, string? AsignadoANombre,
    int? ClienteID, string? Cliente, string? NitCliente, string? TelefonoCliente,
    DateTime FechaCreacion, DateTime FechaActualizacion, DateTime? FechaResolucion,
    string? Notas, int TotalComentarios
);

public record CrearTicketRequest(
    string Titulo, string Descripcion, string Categoria, string Prioridad,
    int? AsignadoA, int? ClienteID, string? Notas
);

public record ActualizarTicketRequest(
    string Titulo, string Descripcion, string Categoria, string Prioridad,
    string Estado, int? AsignadoA, int? ClienteID, string? Notas
);

public record CambiarEstadoTicketRequest(string Estado);

public record TicketComentarioItem(
    int ComentarioID, int TicketID, int AutorID, string? AutorNombre,
    string Texto, bool EsInterno, DateTime Fecha
);

public record CrearTicketComentarioRequest(string Texto, bool EsInterno);

// NPS
public record NpsItem(int EncuestaNPSID, int TicketID, int Puntuacion, string? Comentario, DateTime FechaRespuesta);
public record RegistrarNpsRequest(int Puntuacion, string? Comentario);
public record ResumenNpsItem(double Promedio, int Total, int Promotores, int Pasivos, int Detractores);
