namespace NexoWeb.Common.Dtos;

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

public record ActualizarFotoEmpleadoRequest(string Base64, string ContentType);
public record CrearEmpleadoResponse(int EmpleadoId);

// Lo que cierra EmpleadoDialog: la solicitud de crear/actualizar de siempre,
// mas la foto opcional (mismo patron que ArticuloDialogResultado).
public record EmpleadoDialogResultado(object Datos, string? FotoBase64, string? FotoContentType);

// ---------- Departamentos y Cargos (agosto 2026, RRHH v2) ----------

public record DepartamentoItem(int DepartamentoID, string Nombre, bool Estado, int TotalCargos);
public record CrearDepartamentoRequest(string Nombre);
public record ActualizarDepartamentoRequest(string Nombre, bool Estado);

public record CargoItem(int CargoID, string Nombre, int? DepartamentoID, string? Departamento, bool Estado,
    int? RolPredeterminadoID, string? RolPredeterminado);
public record CrearCargoRequest(string Nombre, int? DepartamentoID, int? RolPredeterminadoID = null);
public record ActualizarCargoRequest(string Nombre, int? DepartamentoID, bool Estado, int? RolPredeterminadoID = null);

// ---------- Historial laboral (agosto 2026, RRHH v2) ----------

public record HistorialLaboralItem(
    int HistorialID, int EmpleadoID, string TipoEvento,
    string? ValorAnterior, string? ValorNuevo, DateTime Fecha, string? Notas, string? Usuario
);

// ---------- Documentos de empleado (agosto 2026, RRHH v2) ----------

public record EmpleadoDocumentoItem(int DocumentoID, int EmpleadoID, string TipoDocumento, string NombreArchivo, string ContentType, DateTime FechaSubida);
public record SubirDocumentoEmpleadoRequest(string TipoDocumento, string NombreArchivo, string ContentType, string Base64);

// ---------- Ausencias y Vacaciones (agosto 2026, RRHH v2) ----------

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

public record EmpleadoSimpleItem(int EmpleadoID, string Nombres, string Apellidos);

// ---------- Asistencia con QR rotativo ----------

public record TokenQrResponse(string Token, int SegundosRestantes);

public record EstadoAsistenciaHoy(
    int EmpleadoID, string Empleado,
    bool TieneEntrada, DateTime? HoraEntrada, string? MetodoEntrada,
    bool TieneSalida, DateTime? HoraSalida, string? MetodoSalida,
    bool RegistraSalida,
    bool TieneAlmuerzo,
    bool TieneEntrada2, DateTime? HoraEntrada2, string? MetodoEntrada2,
    bool TieneSalida2, DateTime? HoraSalida2, string? MetodoSalida2
);

public record MarcarQrRequest(string Token, string Tipo);

// ---------- Horas Extras (oct 2026) ----------

public record HorasExtrasItem(
    int HorasExtrasID, int EmpleadoID,
    DateTime Fecha, decimal CantidadHoras, decimal ValorHora, decimal Total,
    string? Concepto, DateTime FechaRegistro);

public record CrearHorasExtrasRequest(
    DateTime Fecha, decimal CantidadHoras, decimal ValorHora, string? Concepto);

public record ResumenHorasExtras(
    decimal TotalHoras, decimal TotalPagado, int TotalRegistros);
public record MarcarManualRequest(int EmpleadoID, string Tipo, DateTime Hora, string Nota);

// DiaSemana: 1=Lunes...7=Domingo; Semana: null=FIJO, "A"/"B"=SEMANA_AB
public record HorarioDiaItem(
    int DiaSemana, string? Semana,
    TimeOnly HoraEntrada, TimeOnly HoraSalida,
    bool TieneAlmuerzo, TimeOnly? HoraInicioAlmuerzo, TimeOnly? HoraFinAlmuerzo
);

public record HorarioItem(
    int HorarioID, string Nombre, int ToleranciaTardanzaMin,
    string TipoCiclo, bool Activo, bool RegistraSalida,
    List<HorarioDiaItem> Dias
);

public record HorarioDiaInput(
    int DiaSemana, string? Semana,
    TimeSpan HoraEntrada, TimeSpan HoraSalida,
    bool TieneAlmuerzo, TimeSpan? HoraInicioAlmuerzo, TimeSpan? HoraFinAlmuerzo
);

public record CrearHorarioRequest(
    string Nombre, int ToleranciaTardanzaMin, string TipoCiclo, bool RegistraSalida,
    List<HorarioDiaInput> Dias
);

public record AsignarHorarioEmpleadoRequest(int HorarioID, DateTime Desde);

public record RegistroAsistenciaItem(
    int RegistroID, int EmpleadoID, string Empleado, DateTime Fecha,
    DateTime? HoraEntrada, string? MetodoEntrada, string? EntradaRegistradaPor, string? EntradaNota,
    DateTime? HoraSalida, string? MetodoSalida, string? SalidaRegistradaPor, string? SalidaNota,
    int? MinutosTardanza, double? HorasEfectivas,
    DateTime? HoraEntrada2, DateTime? HoraSalida2
);
