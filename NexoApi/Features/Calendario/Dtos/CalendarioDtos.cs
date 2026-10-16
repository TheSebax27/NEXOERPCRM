namespace NexoApi.Features.Calendario.Dtos;

// Fila intermedia para mapear el query principal (sin asistentes).
// Dapper mapea por posicion en records, asi que AsistenteRow va separado.
public record EventoCalendarioRow(
    int EventoID, string Titulo, string TipoEvento,
    DateTime FechaInicio, DateTime FechaFin,
    string? Lugar, string? LinkVirtual, string? Descripcion,
    int? CentroCostoID, string? CentroCosto,
    int CreadoPorUsuarioID, string CreadoPor
);

public record AsistenteRow(int EventoID, int UsuarioID, string Nombre, string Estado);

public record AsistenteEventoItem(int UsuarioID, string Nombre, string Estado);

public record EventoCalendarioItem(
    int EventoID, string Titulo, string TipoEvento,
    DateTime FechaInicio, DateTime FechaFin,
    string? Lugar, string? LinkVirtual, string? Descripcion,
    int? CentroCostoID, string? CentroCosto,
    int CreadoPorUsuarioID, string CreadoPor,
    List<AsistenteEventoItem> Asistentes
);

public record UsuarioDisponibleItem(int UsuarioID, string Nombre, string? Email);

public record CrearEventoRequest(
    string Titulo, string TipoEvento,
    DateTime FechaInicio, DateTime FechaFin,
    string? Lugar, string? LinkVirtual, string? Descripcion,
    int? CentroCostoID, List<int> AsistenteIds
);

public record ActualizarEventoRequest(
    string Titulo, string TipoEvento,
    DateTime FechaInicio, DateTime FechaFin,
    string? Lugar, string? LinkVirtual, string? Descripcion,
    int? CentroCostoID, List<int> AsistenteIds
);
