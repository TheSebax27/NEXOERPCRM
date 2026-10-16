namespace NexoWeb.Common.Dtos;

public record FormularioItem(
    int FormularioID, string Titulo, string? Descripcion, string Token,
    bool Activo, bool AceptaRespuestas, string? MensajeExito,
    int CreadoPor, string? CreadoPorNombre,
    DateTime FechaCreacion, DateTime FechaActualizacion,
    int TotalRespuestas
);

public record FormularioCampoItem(
    int CampoID, int FormularioID, string Tipo, string Etiqueta,
    string? Placeholder, bool Requerido, int Orden, string? Opciones
);

public record FormularioRespuestaItem(
    int RespuestaID, int FormularioID, string Datos,
    string? IPOrigen, DateTime FechaRespuesta
);

public record CrearFormularioRequest(string Titulo, string? Descripcion, string? MensajeExito);

public record ActualizarFormularioRequest(
    string Titulo, string? Descripcion, bool Activo,
    bool AceptaRespuestas, string? MensajeExito
);

public record GuardarCamposRequest(List<CampoGuardarItem> Campos);

public record CampoGuardarItem(
    int? CampoID, string Tipo, string Etiqueta,
    string? Placeholder, bool Requerido, int Orden, string? Opciones
);

public record EnviarRespuestaRequest(int FormularioID, string Datos);

public record FormularioPublicoItem(
    int FormularioID, string Titulo, string? Descripcion,
    bool AceptaRespuestas, string? MensajeExito,
    List<FormularioCampoItem> Campos
);
