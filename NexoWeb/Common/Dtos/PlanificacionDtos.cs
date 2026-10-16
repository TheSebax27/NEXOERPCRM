namespace NexoWeb.Common.Dtos;

public record DemandaProyectadaItem(
    int DemandaID, int ArticuloID, string SkuArticulo, string NombreArticulo,
    int CentroCostoID, string CentroCosto, DateTime Periodo,
    decimal CantidadProyectada, decimal CantidadReal, string? Notas
);

public record CrearDemandaRequest(int ArticuloID, int CentroCostoID, DateTime Periodo, decimal CantidadProyectada, string? Notas);
public record ActualizarDemandaRequest(decimal CantidadProyectada, string? Notas);

public record MetaVentaItem(
    int MetaID, int CentroCostoID, string CentroCosto, DateTime Periodo,
    decimal MetaValor, decimal VentaReal, string? Notas
);

public record CrearMetaVentaRequest(int CentroCostoID, DateTime Periodo, decimal MetaValor, string? Notas);
public record ActualizarMetaVentaRequest(decimal MetaValor, string? Notas);

// ---------- Alertas de desviación (agosto 2026, Planificación v2) ----------
public record DesviacionItem(int CentroCostoID, string CentroCosto, decimal CumplimientoDemanda, decimal CumplimientoVenta);

// ---------- Comparativo histórico multi-mes (agosto 2026, Planificación v2) ----------
public record HistoricoCumplimientoItem(DateTime Periodo, decimal CumplimientoDemandaPromedio, decimal CumplimientoVentaPromedio);

// ---------- Proyección sugerida (agosto 2026, Planificación v2) ----------
public record SugerenciaDemandaItem(decimal CantidadSugerida, int MesesConsiderados);

// ---------- Versionado de metas (agosto 2026, Planificación v2) ----------
public record MetaVentaHistorialItem(int HistorialID, int MetaID, decimal MetaValorAnterior, string? NotasAnterior, DateTime FechaCambio, string? Usuario);

// ---------- Predicción de demanda por media móvil 3 meses ----------
public record PrediccionDemandaItem(
    int ArticuloID, string SKU, string Articulo, string TipoArticulo,
    decimal ConsumoM3, decimal ConsumoM2, decimal ConsumoM1,
    decimal MediaMovil
);
