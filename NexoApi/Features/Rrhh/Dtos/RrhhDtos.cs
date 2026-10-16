namespace NexoApi.Features.Rrhh.Dtos;

// EnAusencia se calcula al vuelo (existe una Ausencia APROBADA que cubre hoy)
// -- no es una columna guardada, para que nunca quede desactualizada. En la
// UI se combina con Estado para mostrar 3 chips: Inactivo (Estado=0),
// Ausente (Estado=1 y EnAusencia=1), Activo (Estado=1 y EnAusencia=0).
public record EmpleadoItem(
    int EmpleadoID, string Nombres, string Apellidos,
    int? CargoID, string? Cargo, int? DepartamentoID, string? Departamento,
    int? CentroCostoID, string? CentroCosto, DateTime? FechaIngreso,
    string? Telefono, string? Email, bool Estado, bool TieneFoto,
    int? JefeDirectoID, string? JefeDirecto, bool EnAusencia, bool TieneUsuario,
    decimal? TarifaHora, string? TipoSangre = null
);

public record CrearEmpleadoRequest(
    string Nombres, string Apellidos, int? CargoID, int? CentroCostoID,
    DateTime? FechaIngreso, string? Telefono, string? Email, int? JefeDirectoID,
    decimal? TarifaHora = null, string? TipoSangre = null
);

public record ActualizarEmpleadoRequest(
    string Nombres, string Apellidos, int? CargoID, int? CentroCostoID,
    DateTime? FechaIngreso, string? Telefono, string? Email, bool Estado, int? JefeDirectoID,
    decimal? TarifaHora = null, string? TipoSangre = null
);

// Foto opcional, una sola por empleado -- mismo patron que la imagen de
// articulo (Catalogo): el UPDATE reemplaza cualquier foto anterior sin
// necesidad de borrarla aparte.
public record ActualizarFotoEmpleadoRequest(string Base64, string ContentType);

// ---------- Departamentos y Cargos (agosto 2026, RRHH v2) ----------

public record DepartamentoItem(int DepartamentoID, string Nombre, bool Estado, int TotalCargos);
public record CrearDepartamentoRequest(string Nombre);
public record ActualizarDepartamentoRequest(string Nombre, bool Estado);

public record CargoItem(int CargoID, string Nombre, int? DepartamentoID, string? Departamento, bool Estado,
    int? RolPredeterminadoID, string? RolPredeterminado);
public record CrearCargoRequest(string Nombre, int? DepartamentoID, int? RolPredeterminadoID = null);
public record ActualizarCargoRequest(string Nombre, int? DepartamentoID, bool Estado, int? RolPredeterminadoID = null);

// ---------- Historial laboral (agosto 2026, RRHH v2) ----------
// Se genera automatico al detectar un cambio de CargoID/CentroCostoID en
// ActualizarEmpleadoAsync -- no hay endpoint para crear entradas a mano.

public record HistorialLaboralItem(
    int HistorialID, int EmpleadoID, string TipoEvento,
    string? ValorAnterior, string? ValorNuevo, DateTime Fecha, string? Notas, string? Usuario
);

// ---------- Documentos de empleado (agosto 2026, RRHH v2) ----------

public record EmpleadoDocumentoItem(int DocumentoID, int EmpleadoID, string TipoDocumento, string NombreArchivo, string ContentType, DateTime FechaSubida);
public record SubirDocumentoEmpleadoRequest(string TipoDocumento, string NombreArchivo, string ContentType, string Base64);

// ---------- Ausencias y Vacaciones (agosto 2026, RRHH v2) ----------
// Solo control de disponibilidad -- sin ningun calculo de nomina.

public record AusenciaItem(
    int AusenciaID, int EmpleadoID, string Empleado, string Tipo,
    DateTime FechaInicio, DateTime FechaFin, string? Motivo, string Estado, DateTime FechaCreacion
);
public record CrearAusenciaRequest(int EmpleadoID, string Tipo, DateTime FechaInicio, DateTime FechaFin, string? Motivo);
public record ActualizarEstadoAusenciaRequest(string Estado);

// ---------- Evaluaciones de desempeño (agosto 2026, RRHH v2) ----------

public record EvaluacionItem(
    int EvaluacionID, int EmpleadoID, int? ResponsableID, string? Responsable,
    DateTime Fecha, decimal Calificacion, string? Comentarios
);
public record CrearEvaluacionRequest(int EmpleadoID, int? ResponsableID, DateTime Fecha, decimal Calificacion, string? Comentarios);

// ---------- Capacitaciones (agosto 2026, RRHH v2) ----------

public record CapacitacionItem(
    int CapacitacionID, int EmpleadoID, string Nombre, string? Institucion,
    DateTime FechaRealizacion, DateTime? FechaVencimiento
);
public record CrearCapacitacionRequest(int EmpleadoID, string Nombre, string? Institucion, DateTime FechaRealizacion, DateTime? FechaVencimiento);

// ---------- Organigrama (agosto 2026, RRHH v2) ----------
// El frontend arma el arbol a partir de esta lista plana (JefeDirectoID).

public record OrganigramaNodo(int EmpleadoID, string Nombres, string Apellidos, string? Cargo, int? JefeDirectoID);

// ---------- Contratos (oct 2026) ----------

public record ContratoItem(
    int ContratoID, int EmpleadoID, string TipoContrato,
    DateTime FechaInicio, DateTime? FechaFin, decimal? SalarioBase,
    string? Notas, string Estado, DateTime FechaCreacion);

public record CrearContratoRequest(
    string TipoContrato, DateTime FechaInicio, DateTime? FechaFin,
    decimal? SalarioBase, string? Notas);

public record ActualizarEstadoContratoRequest(string Estado);

// ---------- Saldo vacaciones (oct 2026) ----------

public record SaldoVacacionesItem(int DiasGanados, int DiasTomados, int DiasDisponibles);

// ---------- Alertas RRHH (oct 2026) ----------

public record AlertaRrhh(string Tipo, string Descripcion, string Empleado, int EmpleadoID, DateTime? FechaReferencia);

// ---------- Export Excel empleados (oct 2026) ----------

public record EmpleadoExportRow(
    int EmpleadoID, string Nombres, string Apellidos,
    string? Cargo, string? Departamento, string? CentroCosto,
    string? FechaIngreso, string? Telefono, string? Email,
    bool Estado, decimal? TarifaHora, string? TipoSangre);
