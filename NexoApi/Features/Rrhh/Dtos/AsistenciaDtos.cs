namespace NexoApi.Features.Rrhh.Dtos;

// DiaSemana: 1=Lunes ... 7=Domingo
// Semana: null=aplica siempre (TipoCiclo=FIJO), "A"/"B" para TipoCiclo=SEMANA_AB
public record HorarioDiaItem(
    int DiaSemana, string? Semana,
    TimeOnly HoraEntrada, TimeOnly HoraSalida,
    bool TieneAlmuerzo, TimeOnly? HoraInicioAlmuerzo, TimeOnly? HoraFinAlmuerzo
);

public record HorarioItem(
    int HorarioID, string Nombre, int ToleranciaTardanzaMin, string TipoCiclo,
    bool Activo, bool RegistraSalida,
    List<HorarioDiaItem> Dias
);

// En el request se usa TimeSpan (compatible con MudTimePicker en web)
public record HorarioDiaInput(
    int DiaSemana, string? Semana,
    TimeSpan HoraEntrada, TimeSpan HoraSalida,
    bool TieneAlmuerzo, TimeSpan? HoraInicioAlmuerzo, TimeSpan? HoraFinAlmuerzo
);

public record CrearHorarioRequest(
    string Nombre, int ToleranciaTardanzaMin, string TipoCiclo, bool RegistraSalida,
    List<HorarioDiaInput> Dias
);

public record AsignarHorarioRequest(int HorarioID, DateTime Desde);

// Modo: 'TTL' = rota cada 5 min | 'SINGLE_USE' = rota al ser escaneado (SegundosRestantes=0)
public record TokenQrResponse(string Token, int SegundosRestantes, string Modo = "TTL");

public record ConfigQrResponse(string Modo);
public record ActualizarModoQrRequest(string Modo);

public record EstadoAsistenciaHoy(
    int EmpleadoID, string Empleado,
    bool TieneEntrada, DateTime? HoraEntrada, string? MetodoEntrada,
    bool TieneSalida, DateTime? HoraSalida, string? MetodoSalida,
    bool RegistraSalida,
    bool TieneAlmuerzo,
    bool TieneEntrada2, DateTime? HoraEntrada2, string? MetodoEntrada2,
    bool TieneSalida2, DateTime? HoraSalida2, string? MetodoSalida2
);

public record EmpleadoSimpleItem(int EmpleadoID, string Nombres, string Apellidos);

public record MarcarQrRequest(string Token, string Tipo);       // Tipo = ENTRADA | SALIDA
public record MarcarManualRequest(int EmpleadoID, string Tipo, DateTime Hora, string Nota);

public record RegistroAsistenciaItem(
    int RegistroID, int EmpleadoID, string Empleado, DateTime Fecha,
    DateTime? HoraEntrada, string? MetodoEntrada, string? EntradaRegistradaPor, string? EntradaNota,
    DateTime? HoraSalida, string? MetodoSalida, string? SalidaRegistradaPor, string? SalidaNota,
    int? MinutosTardanza, double? HorasEfectivas,
    DateTime? HoraEntrada2, DateTime? HoraSalida2
);
