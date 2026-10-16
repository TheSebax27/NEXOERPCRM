namespace NexoApi.Features.Planificacion.Dtos;

// Periodo siempre es el primer dia del mes (ej. 2026-08-01).
// CantidadReal: suma de Kardex ENTRADA_PT para ese Articulo+CentroCosto+mes.
public record DemandaProyectadaItem(
    int DemandaID, int ArticuloID, string SkuArticulo, string NombreArticulo,
    int CentroCostoID, string CentroCosto, DateTime Periodo,
    decimal CantidadProyectada, decimal CantidadReal, string? Notas
);

public record CrearDemandaRequest(int ArticuloID, int CentroCostoID, DateTime Periodo, decimal CantidadProyectada, string? Notas);
public record ActualizarDemandaRequest(decimal CantidadProyectada, string? Notas);

// VentaReal: Cantidad * PrecioVenta actual del articulo, sumado sobre Kardex
// SALIDA_VENTA_VISIONS de ese CentroCosto+mes -- aproximacion (usa el precio
// de venta VIGENTE, no el que tenia el articulo al momento de cada venta).
public record MetaVentaItem(
    int MetaID, int CentroCostoID, string CentroCosto, DateTime Periodo,
    decimal MetaValor, decimal VentaReal, string? Notas
);

public record CrearMetaVentaRequest(int CentroCostoID, DateTime Periodo, decimal MetaValor, string? Notas);
public record ActualizarMetaVentaRequest(decimal MetaValor, string? Notas);

// ---------- Alertas de desviación (agosto 2026, Planificación v2) ----------
// Mismo patrón de "alerta interna" que Clientes Fríos (CRM) -- solo detecta,
// no envía nada. Centro de Costo cuyo cumplimiento del mes actual está por
// debajo del umbral, en Demanda y/o Venta.
public record DesviacionItem(int CentroCostoID, string CentroCosto, decimal CumplimientoDemanda, decimal CumplimientoVenta);

// ---------- Comparativo histórico multi-mes (agosto 2026, Planificación v2) ----------
public record HistoricoCumplimientoItem(DateTime Periodo, decimal CumplimientoDemandaPromedio, decimal CumplimientoVentaPromedio);

// ---------- Proyección sugerida (agosto 2026, Planificación v2) ----------
// Promedio de lo realmente producido (ENTRADA_PT) en los últimos N meses
// completos para ese Articulo+CentroCosto -- el usuario la acepta o la edita,
// nunca se guarda automáticamente.
public record SugerenciaDemandaItem(decimal CantidadSugerida, int MesesConsiderados);

// ---------- Versionado de metas (agosto 2026, Planificación v2) ----------
// Se inserta automático en ActualizarMetaVentaAsync, con el valor ANTERIOR,
// antes de sobrescribir -- no hay endpoint para crear entradas a mano.
public record MetaVentaHistorialItem(int HistorialID, int MetaID, decimal MetaValorAnterior, string? NotasAnterior, DateTime FechaCambio, string? Usuario);

// ---------- Predicción de demanda por media móvil 3 meses ----------
// ConsumoM1 = mes más reciente completo, ConsumoM3 = el más antiguo.
// MediaMovil = (M1+M2+M3)/3 — sugerencia de cantidad para el próximo mes.
// Solo incluye artículos con al menos un movimiento de salida en los 3 meses.
public record PrediccionDemandaItem(
    int ArticuloID, string SKU, string Articulo, string TipoArticulo,
    decimal ConsumoM3, decimal ConsumoM2, decimal ConsumoM1,
    decimal MediaMovil
);
