namespace NexoApi.Features.Recetas.Dtos;

public record MaquinariaRecetaInput(int MaquinariaID, decimal? HorasEstimadasPorLote, string? Notas);

public record DetalleRecetaRequest(
    int InsumoID,
    decimal CantidadRequerida,
    decimal PorcentajeMermaEstandar,
    int? CentroTrabajoID,
    int Orden
);

public record CrearRecetaRequest(
    int ProductoTerminadoID,
    string NombreReceta,
    decimal CantidadRendimientoBase,
    List<DetalleRecetaRequest> Detalle,
    List<MaquinariaRecetaInput>? Maquinas = null
);

public record CrearNuevaVersionRequest(
    string NombreReceta,
    decimal CantidadRendimientoBase,
    List<DetalleRecetaRequest> Detalle,
    List<MaquinariaRecetaInput>? Maquinas = null
);

public record RecetaResumen(
    int RecetaID,
    int ProductoTerminadoID,
    string ProductoTerminado,
    string NombreReceta,
    int Version,
    decimal CantidadRendimientoBase,
    string UnidadRendimiento,
    bool Estado
);

public record RecetaDetalleItem(
    int RecetaDetalleID,
    int InsumoID,
    string Insumo,
    decimal CantidadRequerida,
    string Unidad,
    decimal PorcentajeMermaEstandar,
    int? CentroTrabajoID,
    int Orden
);
