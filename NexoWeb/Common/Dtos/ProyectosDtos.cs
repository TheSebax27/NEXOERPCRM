namespace NexoWeb.Common.Dtos;

public record ProyectoItem(
    int ProyectoID, string Nombre, int? ClienteID, string? Cliente,
    int? CentroCostoID, string? CentroCosto, string? Descripcion,
    DateTime FechaInicio, DateTime? FechaFin, string Estado, decimal Presupuesto,
    string Prioridad, decimal CostoTotal, int TotalTareas, int TareasCompletadas,
    int TotalHitos, int HitosCompletados
);

public record CrearProyectoRequest(
    string Nombre, int? ClienteID, int? CentroCostoID, string? Descripcion,
    DateTime FechaInicio, DateTime? FechaFin, decimal Presupuesto, string Prioridad
);

public record ActualizarProyectoRequest(
    string Nombre, int? ClienteID, int? CentroCostoID, string? Descripcion,
    DateTime FechaInicio, DateTime? FechaFin, string Estado, decimal Presupuesto, string Prioridad
);

public record TareaItem(
    int TareaID, int ProyectoID, string Titulo, int? ResponsableID, string? Responsable,
    string Estado, DateTime? FechaLimite, List<int> DependeDeTareaIds
);
public record CrearTareaRequest(int ProyectoID, string Titulo, int? ResponsableID, DateTime? FechaLimite, List<int>? DependeDeTareaIds);
public record ActualizarTareaRequest(string Titulo, int? ResponsableID, string Estado, DateTime? FechaLimite, List<int>? DependeDeTareaIds);

public record CostoItem(
    int CostoID, int ProyectoID, string Tipo, string Descripcion, decimal Valor,
    int? EmpleadoID, string? Empleado, int? ArticuloID, string? Articulo, DateTime Fecha
);
public record CrearCostoRequest(int ProyectoID, string Tipo, string Descripcion, decimal Valor, int? EmpleadoID, int? ArticuloID);

// ---------- Hitos / Fases (agosto 2026, Proyectos v2) ----------
public record HitoItem(int HitoID, int ProyectoID, string Nombre, DateTime FechaObjetivo, DateTime? FechaCompletado, string Estado, int Orden);
public record CrearHitoRequest(int ProyectoID, string Nombre, DateTime FechaObjetivo, int Orden);
public record ActualizarHitoRequest(string Nombre, DateTime FechaObjetivo, string Estado, int Orden);

// ---------- Documentos (agosto 2026, Proyectos v2) ----------
public record ProyectoDocumentoItem(int DocumentoID, int ProyectoID, string TipoDocumento, string NombreArchivo, string ContentType, DateTime FechaSubida);
public record SubirDocumentoProyectoRequest(string TipoDocumento, string NombreArchivo, string ContentType, string Base64);

// ---------- Comentarios / Bitacora (agosto 2026, Proyectos v2) ----------
public record ComentarioProyectoItem(int ComentarioID, int ProyectoID, string Texto, DateTime Fecha, string? Usuario);
public record CrearComentarioRequest(int ProyectoID, string Texto);

// ---------- Alertas (agosto 2026, Proyectos v2) ----------
public record ProyectoAlertaItem(int ProyectoID, string Nombre, bool Atrasado, bool ConSobrecosto, DateTime? FechaFin, decimal Presupuesto, decimal CostoTotal);
