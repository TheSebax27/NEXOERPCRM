namespace NexoWeb.Common.Dtos;

public record AsistenteEventoItem(int UsuarioID, string Nombre, string Estado);

public record EventoCalendarioItem(
    int EventoID, string Titulo, string TipoEvento,
    DateTime FechaInicio, DateTime FechaFin,
    string? Lugar, string? LinkVirtual, string? Descripcion,
    int? CentroCostoID, string? CentroCosto,
    int CreadoPorUsuarioID, string CreadoPor,
    List<AsistenteEventoItem> Asistentes
)
{
    public bool EsHoy => FechaInicio.ToLocalTime().Date == NexoWeb.Common.ColombiaClock.Today;
    public bool EsProximo => FechaInicio.ToLocalTime() > NexoWeb.Common.ColombiaClock.Now;
    public TimeSpan TiempoHasta => FechaInicio.ToLocalTime() - NexoWeb.Common.ColombiaClock.Now;

    public string ColorTipo => TipoEvento switch
    {
        "Reunion"      => "#7C5CFF",
        "Deadline"     => "#F59E0B",
        "Capacitacion" => "#10B981",
        "Entrega"      => "#3B82F6",
        "Urgente"      => "#EF4444",
        _              => "#9B9BB4"
    };
}

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
