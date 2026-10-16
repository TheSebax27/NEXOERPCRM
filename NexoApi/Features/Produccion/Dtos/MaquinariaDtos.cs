namespace NexoApi.Features.Produccion.Dtos;

public record TipoMaquinariaItem(int TipoMaquinariaID, string Nombre, string? Descripcion, bool Activo);

public record MaquinariaItem(
    int MaquinariaID, string Codigo, string Nombre,
    int TipoMaquinariaID, string TipoMaquinaria,
    int? CentroTrabajoID, string? CentroTrabajo,
    string Estado,
    string? Marca, string? Modelo,
    decimal? CostoHoraOperacion,
    DateOnly? ProximoMantenimiento,
    bool MantenimientoVencido,
    bool TieneFoto = false
);

public record MaquinariaDetalle(
    int MaquinariaID, string Codigo, string Nombre,
    int TipoMaquinariaID, string TipoMaquinaria,
    int? CentroTrabajoID, string? CentroTrabajo,
    string Estado,
    string? Marca, string? Modelo, string? NumeroSerie,
    DateOnly? FechaAdquisicion, int? VidaUtilAnios,
    decimal? CostoAdquisicion, decimal? CostoHoraOperacion,
    decimal? CapacidadMaxima, string? UnidadCapacidad,
    string? UbicacionFisica, string? Notas,
    DateTime FechaCreacion,
    bool TieneFoto = false
);

public record ActualizarFotoMaquinariaRequest(string Base64, string ContentType);

public record MantenimientoItem(
    int MantenimientoID, int MaquinariaID, string Maquinaria,
    string TipoMantenimiento, DateOnly FechaRealizado,
    string Descripcion, decimal? Costo,
    decimal? HorasFueraServicio, string? Tecnico,
    DateOnly? ProximoMantenimiento, string? Observaciones,
    string? Usuario, DateTime FechaRegistro
);

public record MaquinariaEnMantenimientoItem(
    int MaquinariaID, string Codigo, string Nombre,
    int? CentroTrabajoID, string? CentroTrabajo
);

public record RecetaMaquinariaItem(
    int MaquinariaID, string Codigo, string Nombre, string TipoMaquinaria,
    decimal? HorasEstimadasPorLote, string? Notas
);

public record OrdenMaquinariaItem(
    int MaquinariaID, string Codigo, string Nombre, string TipoMaquinaria,
    decimal? HorasReales, string? Notas
);

public record MaquinariaEstadisticas(
    int TotalOrdenes, decimal TotalHorasOrdenes, decimal? PromedioHorasPorOrden,
    int TotalRecetas
);

// ── Requests ──────────────────────────────────────────────────

public record CrearTipoMaquinariaRequest(string Nombre, string? Descripcion);

public record CrearMaquinariaRequest(
    string Codigo, string Nombre, int TipoMaquinariaID,
    int? CentroTrabajoID,
    string? Marca, string? Modelo, string? NumeroSerie,
    DateOnly? FechaAdquisicion, int? VidaUtilAnios,
    decimal? CostoAdquisicion, decimal? CostoHoraOperacion,
    decimal? CapacidadMaxima, string? UnidadCapacidad,
    string? UbicacionFisica, string? Notas
);

public record ActualizarMaquinariaRequest(
    string Codigo, string Nombre, int TipoMaquinariaID,
    int? CentroTrabajoID, string Estado,
    string? Marca, string? Modelo, string? NumeroSerie,
    DateOnly? FechaAdquisicion, int? VidaUtilAnios,
    decimal? CostoAdquisicion, decimal? CostoHoraOperacion,
    decimal? CapacidadMaxima, string? UnidadCapacidad,
    string? UbicacionFisica, string? Notas
);

public record CrearMantenimientoRequest(
    string TipoMantenimiento,
    DateOnly FechaRealizado,
    string Descripcion,
    decimal? Costo,
    decimal? HorasFueraServicio,
    string? Tecnico,
    DateOnly? ProximoMantenimiento,
    string? Observaciones,
    // Cambiar estado de la máquina al registrar mantenimiento (opcional)
    string? NuevoEstadoMaquinaria
);
