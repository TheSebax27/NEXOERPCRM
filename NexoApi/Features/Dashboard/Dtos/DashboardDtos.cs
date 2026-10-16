namespace NexoApi.Features.Dashboard.Dtos;

public record PlanVsRealPunto(DateTime Fecha, decimal TotalPlanificado, decimal TotalReal);

public record DistribucionCentroCostoItem(
    int CentroCostoID, string CentroCosto, int TotalOrdenes, decimal TotalUnidadesProducidas, decimal InversionTotal
);

public record PerdidaPorMotivoItem(string Motivo, decimal CantidadTotalPerdida, decimal ValorTotalPerdido);

public record TendenciaCostoPunto(DateTime Fecha, decimal CostoUnitarioReal);

public record CumplimientoCentroCostoItem(
    int CentroCostoID,
    string CentroCosto,
    int TotalOrdenesFinalizadas,
    int OrdenesATiempo,
    decimal PorcentajeCumplimiento
);

// ---------- Resumenes transversales (BI, agosto 2026) ----------

public record ResumenCrmItem(int ClientesNuevos, int Interacciones, int CotizacionesTotal = 0, int Convertidas = 0)
{
    public decimal TasaConversion => CotizacionesTotal == 0 ? 0m : Math.Round(Convertidas * 100m / CotizacionesTotal, 1);
}

public record EmpleadosPorCentroCostoItem(string CentroCosto, int TotalEmpleados);

public record ResumenPlanificacionItem(decimal CumplimientoDemandaPromedio, decimal CumplimientoVentaPromedio);

public record ResumenInventarioItem(decimal ValorTotalStock, int ArticulosConAlerta);

public record ResumenMaquinariaItem(
    int TotalMaquinas,
    int EnMantenimiento,
    int MantenimientoVencido,
    decimal CostoMantenimientoMes
);

public record VentasPorDepartamentoItem(string Departamento, decimal TotalVentas, int CantidadClientes);

// ---------- Analisis de Facturacion (BI workspace, agosto 2026) ----------

public record ResumenFacturacionItem(
    decimal TotalEmitido, decimal TotalCobrado, decimal SaldoPendiente, int DocumentosEmitidos
);

public record IngresoPorMesPunto(int Anio, int Mes, string NombreMes, decimal TotalFacturado, decimal TotalCobrado);

public record TopClienteItem(int ClienteID, string Cliente, string? NIT, decimal TotalFacturado, int NumDocumentos);

public record TopArticuloItem(int ArticuloID, string SKU, string Nombre, decimal CantidadVendida, decimal TotalFacturado);

// ---------- Analisis Financiero — margen por artículo ----------
public record MargenArticuloItem(
    int ArticuloID, string SKU, string Nombre,
    decimal CantidadVendida, decimal TotalFacturado,
    decimal CostoEstimado, decimal MargenBruto, decimal PorcentajeMargen
);

// ---------- Comparativa año a año ----------
public record ComparativaMesItem(
    int Anio, int Mes, string NombreMes,
    decimal TotalActual, decimal TotalAnterior,
    decimal VariacionPct
);

// ---------- Feed de actividad reciente ----------
public record ActividadItem(
    string Tipo,
    string Descripcion,
    string Entidad,
    DateTime FechaHora,
    string Link,
    string Icono,
    string Color
);

// ---------- Sparklines — 7 días de tendencia para KPIs ----------
public record SparklineItem(DateTime Fecha, int Valor);
public record SparklinesDashboard(
    IEnumerable<SparklineItem> Facturas,
    IEnumerable<SparklineItem> Ordenes,
    IEnumerable<SparklineItem> Cotizaciones
);

// ---------- Alertas de stock bajo mínimo ----------
public record AlertaStockItem(
    int ArticuloID, string SKU, string Articulo,
    string CentroCosto, string Bodega,
    decimal CantidadActual, decimal StockMinimo,
    decimal Deficit
);

// ---------- Inteligencia de ventas ----------
public record HorarioPicoItem(int Hora, int DiaSemana, int Total);
public record ParComplementarioItem(int ArticuloAID, string ArticuloANombre, int ArticuloBID, string ArticuloBNombre, int Veces);