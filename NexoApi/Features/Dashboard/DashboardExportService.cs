using ClosedXML.Excel;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Catalogo;
using NexoApi.Features.Catalogo.Dtos;
using NexoApi.Features.Crm;
using NexoApi.Features.Crm.Dtos;
using NexoApi.Features.Dashboard.Dtos;
using NexoApi.Features.Inventario;
using NexoApi.Features.Inventario.Dtos;
using NexoApi.Features.Planificacion;
using NexoApi.Features.Planificacion.Dtos;
using NexoApi.Features.Proyectos;
using NexoApi.Features.Proyectos.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NexoApi.Features.Dashboard;

public interface IDashboardExportService
{
    Task<byte[]> GenerarExcelAsync(DateTime desde, DateTime hasta);
    Task<byte[]> GenerarPdfAsync(DateTime desde, DateTime hasta);
}

// Junta los mismos datos que ya muestra el Dashboard (Plan vs Real, distribucion,
// cumplimiento, perdidas) mas el catalogo/stock actual, y los arma en un Excel
// (ClosedXML) o un PDF (QuestPDF) con la misma paleta morada de la app. A
// proposito no incluye imagenes de articulos -- son reportes de datos, no
// catalogos visuales, y las imagenes solo agregarian peso sin aportar nada aqui.
public class DashboardExportService : IDashboardExportService
{
    // Paleta "Morado + Gris moderno" de NEXO (ver CLAUDE.md #10), reutilizada
    // aqui para que los reportes se sientan parte de la misma aplicacion.
    private static readonly string ColorPrimario = "#7C5CFF";
    private static readonly string ColorPrimarioOscuro = "#5A3BFF";
    private static readonly string ColorTextoOscuro = "#1A1C2E";
    private static readonly string ColorTextoSecundario = "#6B6B8A";
    private static readonly string ColorBordes = "#E8E9EF";
    private static readonly string ColorFondoClaro = "#F4F5F9";
    private static readonly string ColorExito = "#10B981";
    private static readonly string ColorError = "#EF4444";
    private static readonly string ColorInfo = "#0EA5E9";

    private readonly IDbConnectionFactory _db;
    private readonly IDashboardService _dashboardService;
    private readonly ICatalogoService _catalogoService;
    private readonly IInventarioService _inventarioService;
    private readonly ICrmService _crmService;
    private readonly IPlanificacionService _planificacionService;
    private readonly IProyectosService _proyectosService;

    public DashboardExportService(
        IDbConnectionFactory db, IDashboardService dashboardService,
        ICatalogoService catalogoService, IInventarioService inventarioService,
        ICrmService crmService, IPlanificacionService planificacionService,
        IProyectosService proyectosService)
    {
        _db = db;
        _dashboardService = dashboardService;
        _catalogoService = catalogoService;
        _inventarioService = inventarioService;
        _crmService = crmService;
        _planificacionService = planificacionService;
        _proyectosService = proyectosService;
    }

    private record DatosReporte(
        string NombreEmpresa,
        string? NombrePropietario,
        List<PlanVsRealPunto> PlanVsReal,
        List<DistribucionCentroCostoItem> Distribucion,
        List<CumplimientoCentroCostoItem> Cumplimiento,
        List<PerdidaPorMotivoItem> Perdidas,
        List<StockConsolidadoItem> Stock,
        List<ArticuloItem> Articulos,
        ResumenCrmItem? ResumenCrm,
        List<ClienteFrioItem> ClientesFrios,
        List<DesviacionItem> Desviaciones,
        List<ProyectoAlertaItem> AlertasProyectos
    );

    private async Task<DatosReporte> RecolectarDatosAsync(DateTime desde, DateTime hasta)
    {
        using var connection = _db.CreateConnection();
        var (nombreEmpresa, nombrePropietario) = await connection.QueryFirstOrDefaultAsync<(string, string?)>(
            "SELECT NombreEmpresa, NombrePropietario FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");
        if (string.IsNullOrEmpty(nombreEmpresa)) nombreEmpresa = "NEXO ERP";

        var planVsReal = (await _dashboardService.ObtenerPlanVsRealAsync(desde, hasta)).ToList();
        var distribucion = (await _dashboardService.ObtenerDistribucionCentroCostoAsync()).ToList();
        var cumplimiento = (await _dashboardService.ObtenerCumplimientoAsync()).ToList();
        var perdidas = (await _dashboardService.ObtenerPerdidasPorMotivoAsync(desde, hasta)).ToList();
        var stock = (await _inventarioService.ConsultarStockAsync(null, null, null)).ToList();
        var articulos = (await _catalogoService.ListarArticulosAsync(null, null, tamano: 5000)).Items;

        ResumenCrmItem? resumenCrm = null;
        List<ClienteFrioItem> clientesFrios = new();
        List<DesviacionItem> desviaciones = new();
        List<ProyectoAlertaItem> alertasProyectos = new();

        try { resumenCrm = await _dashboardService.ObtenerResumenCrmAsync(desde, hasta); } catch { }
        try { clientesFrios = (await _crmService.ListarClientesFriosAsync(30)).ToList(); } catch { }
        try { desviaciones = (await _planificacionService.ListarDesviacionesAsync(70)).ToList(); } catch { }
        try { alertasProyectos = (await _proyectosService.ListarAlertasAsync()).ToList(); } catch { }

        return new DatosReporte(nombreEmpresa, nombrePropietario, planVsReal, distribucion, cumplimiento, perdidas, stock, articulos,
            resumenCrm, clientesFrios, desviaciones, alertasProyectos);
    }

    // ==================================================================
    // EXCEL (ClosedXML)
    // ==================================================================

    public async Task<byte[]> GenerarExcelAsync(DateTime desde, DateTime hasta)
    {
        var datos = await RecolectarDatosAsync(desde, hasta);

        using var libro = new XLWorkbook();

        CrearHojaResumen(libro, datos, desde, hasta);
        CrearHojaPlanVsReal(libro, datos);
        CrearHojaDistribucion(libro, datos);
        CrearHojaCumplimiento(libro, datos);
        CrearHojaDesviaciones(libro, datos);
        CrearHojaPerdidas(libro, datos);
        CrearHojaStock(libro, datos);
        CrearHojaArticulos(libro, datos);
        CrearHojaCrm(libro, datos);
        CrearHojaAlertasProyectos(libro, datos);

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return stream.ToArray();
    }

    private void EstilarEncabezado(IXLWorksheet hoja, int fila, int columnas, string colorHex)
    {
        var rango = hoja.Range(fila, 1, fila, columnas);
        rango.Style.Fill.BackgroundColor = XLColor.FromHtml(colorHex);
        rango.Style.Font.FontColor = XLColor.White;
        rango.Style.Font.Bold = true;
        rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hoja.Row(fila).Height = 22;
    }

    private void EstilarTitulo(IXLWorksheet hoja, string titulo, string nombreEmpresa, string? propietario = null)
    {
        hoja.Cell(1, 1).Value = propietario is not null
            ? $"{nombreEmpresa}  ·  {propietario}"
            : nombreEmpresa;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 16;
        hoja.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml(ColorPrimarioOscuro);

        hoja.Cell(2, 1).Value = titulo;
        hoja.Cell(2, 1).Style.Font.FontSize = 12;
        hoja.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml(ColorTextoSecundario);

        hoja.Cell(3, 1).Value = $"Generado: {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}";
        hoja.Cell(3, 1).Style.Font.FontSize = 9;
        hoja.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml(ColorTextoSecundario);
        hoja.Cell(3, 1).Style.Font.Italic = true;
    }

    private void CrearHojaResumen(XLWorkbook libro, DatosReporte d, DateTime desde, DateTime hasta)
    {
        var hoja = libro.Worksheets.Add("Resumen");
        EstilarTitulo(hoja, $"Reporte de Producción e Inventario ({desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy})", d.NombreEmpresa, d.NombrePropietario);

        var totalPlanificado = d.PlanVsReal.Sum(p => p.TotalPlanificado);
        var totalReal = d.PlanVsReal.Sum(p => p.TotalReal);
        var cumplimientoGlobal = totalPlanificado > 0 ? totalReal / totalPlanificado * 100 : 0;
        var totalPerdidas = d.Perdidas.Sum(p => p.ValorTotalPerdido);
        var valorStock = d.Stock.Sum(s => s.ValorTotal);

        var kpis = new (string Titulo, string Valor, string Color)[]
        {
            ("Total Planificado", totalPlanificado.ToString("N0"), ColorInfo),
            ("Total Real Producido", totalReal.ToString("N0"), ColorPrimario),
            ("Cumplimiento Global", $"{cumplimientoGlobal:N1}%", cumplimientoGlobal >= 100 ? ColorExito : ColorError),
            ("Pérdidas (período)", totalPerdidas.ToString("C0"), ColorError),
            ("Valor de Stock Actual", valorStock.ToString("C0"), ColorExito),
            ("Artículos en Catálogo", d.Articulos.Count.ToString(), ColorTextoSecundario),
            ("Clientes Nuevos (CRM)", d.ResumenCrm?.ClientesNuevos.ToString() ?? "-", ColorInfo),
            ("Interacciones CRM", d.ResumenCrm?.Interacciones.ToString() ?? "-", ColorPrimario),
            ("Clientes sin contacto (+30d)", d.ClientesFrios.Count.ToString(), d.ClientesFrios.Count > 0 ? ColorError : ColorExito),
            ("CC con desviación (<70%)", d.Desviaciones.Count.ToString(), d.Desviaciones.Count > 0 ? "#F59E0B" : ColorExito),
            ("Proyectos atrasados", d.AlertasProyectos.Count(p => p.Atrasado).ToString(), d.AlertasProyectos.Any(p => p.Atrasado) ? ColorError : ColorExito),
            ("Proyectos con sobrecosto", d.AlertasProyectos.Count(p => p.ConSobrecosto).ToString(), d.AlertasProyectos.Any(p => p.ConSobrecosto) ? ColorError : ColorExito),
        };

        var filaBase = 5;
        for (var i = 0; i < kpis.Length; i++)
        {
            var fila = filaBase + i;
            hoja.Cell(fila, 1).Value = kpis[i].Titulo;
            hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml(ColorTextoSecundario);
            hoja.Cell(fila, 2).Value = kpis[i].Valor;
            hoja.Cell(fila, 2).Style.Font.Bold = true;
            hoja.Cell(fila, 2).Style.Font.FontSize = 13;
            hoja.Cell(fila, 2).Style.Font.FontColor = XLColor.FromHtml(kpis[i].Color);
        }

        hoja.Column(1).Width = 26;
        hoja.Column(2).Width = 20;
    }

    private void CrearHojaPlanVsReal(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("Plan vs Real");
        EstilarTitulo(hoja, "Producción: Planificado vs Real por día", d.NombreEmpresa);

        var filaEncabezado = 5;
        hoja.Cell(filaEncabezado, 1).Value = "Fecha";
        hoja.Cell(filaEncabezado, 2).Value = "Planificado";
        hoja.Cell(filaEncabezado, 3).Value = "Real";
        hoja.Cell(filaEncabezado, 4).Value = "Variación %";
        EstilarEncabezado(hoja, filaEncabezado, 4, ColorPrimarioOscuro);

        var fila = filaEncabezado + 1;
        foreach (var p in d.PlanVsReal)
        {
            var variacion = p.TotalPlanificado > 0 ? (p.TotalReal - p.TotalPlanificado) / p.TotalPlanificado * 100 : 0;

            hoja.Cell(fila, 1).Value = p.Fecha;
            hoja.Cell(fila, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            hoja.Cell(fila, 2).Value = (double)p.TotalPlanificado;
            hoja.Cell(fila, 3).Value = (double)p.TotalReal;
            hoja.Cell(fila, 4).Value = (double)variacion / 100;
            hoja.Cell(fila, 4).Style.NumberFormat.Format = "+0.0%;-0.0%";
            hoja.Cell(fila, 4).Style.Font.FontColor = XLColor.FromHtml(variacion >= 0 ? ColorExito : ColorError);
            hoja.Cell(fila, 4).Style.Font.Bold = true;
            fila++;
        }

        AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, 4);
        hoja.Columns(1, 4).AdjustToContents();
    }

    private void CrearHojaDistribucion(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("Distribución CC");
        EstilarTitulo(hoja, "Distribución de Producción por Centro de Costo", d.NombreEmpresa);

        var filaEncabezado = 5;
        hoja.Cell(filaEncabezado, 1).Value = "Centro de Costo";
        hoja.Cell(filaEncabezado, 2).Value = "Órdenes";
        hoja.Cell(filaEncabezado, 3).Value = "Unidades Producidas";
        hoja.Cell(filaEncabezado, 4).Value = "Inversión";
        EstilarEncabezado(hoja, filaEncabezado, 4, ColorPrimarioOscuro);

        var fila = filaEncabezado + 1;
        foreach (var item in d.Distribucion)
        {
            hoja.Cell(fila, 1).Value = item.CentroCosto;
            hoja.Cell(fila, 2).Value = item.TotalOrdenes;
            hoja.Cell(fila, 3).Value = (double)item.TotalUnidadesProducidas;
            hoja.Cell(fila, 4).Value = (double)item.InversionTotal;
            hoja.Cell(fila, 4).Style.NumberFormat.Format = "$#,##0";
            fila++;
        }

        AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, 4);
        hoja.Columns(1, 4).AdjustToContents();
    }

    private void CrearHojaCumplimiento(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("Cumplimiento CC");
        EstilarTitulo(hoja, "Cumplimiento de Planificación por Centro de Costo", d.NombreEmpresa);

        var filaEncabezado = 5;
        hoja.Cell(filaEncabezado, 1).Value = "Centro de Costo";
        hoja.Cell(filaEncabezado, 2).Value = "Órdenes Finalizadas";
        hoja.Cell(filaEncabezado, 3).Value = "Órdenes a Tiempo";
        hoja.Cell(filaEncabezado, 4).Value = "% Cumplimiento";
        EstilarEncabezado(hoja, filaEncabezado, 4, ColorPrimarioOscuro);

        var fila = filaEncabezado + 1;
        foreach (var item in d.Cumplimiento)
        {
            hoja.Cell(fila, 1).Value = item.CentroCosto;
            hoja.Cell(fila, 2).Value = item.TotalOrdenesFinalizadas;
            hoja.Cell(fila, 3).Value = item.OrdenesATiempo;
            hoja.Cell(fila, 4).Value = (double)item.PorcentajeCumplimiento / 100;
            hoja.Cell(fila, 4).Style.NumberFormat.Format = "0.0%";
            hoja.Cell(fila, 4).Style.Font.FontColor = XLColor.FromHtml(item.PorcentajeCumplimiento >= 90 ? ColorExito : item.PorcentajeCumplimiento >= 70 ? "#F59E0B" : ColorError);
            hoja.Cell(fila, 4).Style.Font.Bold = true;
            fila++;
        }

        AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, 4);
        hoja.Columns(1, 4).AdjustToContents();
    }

    private void CrearHojaPerdidas(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("Pérdidas");
        EstilarTitulo(hoja, "Pérdidas de Inventario por Motivo", d.NombreEmpresa);

        var filaEncabezado = 5;
        hoja.Cell(filaEncabezado, 1).Value = "Motivo";
        hoja.Cell(filaEncabezado, 2).Value = "Cantidad Perdida";
        hoja.Cell(filaEncabezado, 3).Value = "Valor Perdido";
        EstilarEncabezado(hoja, filaEncabezado, 3, ColorError);

        var fila = filaEncabezado + 1;
        foreach (var item in d.Perdidas)
        {
            hoja.Cell(fila, 1).Value = item.Motivo;
            hoja.Cell(fila, 2).Value = (double)item.CantidadTotalPerdida;
            hoja.Cell(fila, 3).Value = (double)item.ValorTotalPerdido;
            hoja.Cell(fila, 3).Style.NumberFormat.Format = "$#,##0";
            fila++;
        }

        AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, 3);
        hoja.Columns(1, 3).AdjustToContents();
    }

    private void CrearHojaStock(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("Stock Actual");
        EstilarTitulo(hoja, "Stock Consolidado Actual", d.NombreEmpresa);

        var filaEncabezado = 5;
        string[] columnas = { "SKU", "Artículo", "Bodega", "Centro de Costo", "Cantidad", "Unidad", "Costo Unitario", "Valor Total", "Requiere Pedido" };
        for (var c = 0; c < columnas.Length; c++)
            hoja.Cell(filaEncabezado, c + 1).Value = columnas[c];
        EstilarEncabezado(hoja, filaEncabezado, columnas.Length, ColorPrimarioOscuro);

        var fila = filaEncabezado + 1;
        foreach (var s in d.Stock)
        {
            hoja.Cell(fila, 1).Value = s.SKU;
            hoja.Cell(fila, 2).Value = s.Articulo;
            hoja.Cell(fila, 3).Value = s.Bodega;
            hoja.Cell(fila, 4).Value = s.CentroCosto;
            hoja.Cell(fila, 5).Value = (double)s.CantidadActual;
            hoja.Cell(fila, 6).Value = s.Unidad;
            hoja.Cell(fila, 7).Value = (double)s.CostoUnitarioLote;
            hoja.Cell(fila, 7).Style.NumberFormat.Format = "$#,##0.00";
            hoja.Cell(fila, 8).Value = (double)s.ValorTotal;
            hoja.Cell(fila, 8).Style.NumberFormat.Format = "$#,##0";
            hoja.Cell(fila, 9).Value = s.RequierePedido ? "Sí" : "No";
            if (s.RequierePedido)
                hoja.Cell(fila, 9).Style.Font.FontColor = XLColor.FromHtml("#F59E0B");
            fila++;
        }

        AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, columnas.Length);
        hoja.Columns(1, columnas.Length).AdjustToContents();
        hoja.SheetView.FreezeRows(filaEncabezado);
    }

    private void CrearHojaArticulos(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("Artículos");
        EstilarTitulo(hoja, "Catálogo de Artículos", d.NombreEmpresa);

        var filaEncabezado = 5;
        string[] columnas = { "SKU", "Nombre", "Tipo", "Unidad", "Costo Promedio", "Precio Venta", "Stock Mínimo", "Punto Reorden", "Estado" };
        for (var c = 0; c < columnas.Length; c++)
            hoja.Cell(filaEncabezado, c + 1).Value = columnas[c];
        EstilarEncabezado(hoja, filaEncabezado, columnas.Length, ColorPrimarioOscuro);

        var fila = filaEncabezado + 1;
        foreach (var a in d.Articulos)
        {
            hoja.Cell(fila, 1).Value = a.Referencia;
            hoja.Cell(fila, 2).Value = a.Nombre;
            hoja.Cell(fila, 3).Value = a.TipoArticulo;
            hoja.Cell(fila, 4).Value = a.Unidad;
            hoja.Cell(fila, 5).Value = (double)a.CostoPromedio;
            hoja.Cell(fila, 5).Style.NumberFormat.Format = "$#,##0.00";
            hoja.Cell(fila, 6).Value = (double)(a.PPublico ?? 0);
            hoja.Cell(fila, 6).Style.NumberFormat.Format = "$#,##0.00";
            hoja.Cell(fila, 7).Value = (double)a.StockMinimo;
            hoja.Cell(fila, 8).Value = (double)a.PuntoReorden;
            hoja.Cell(fila, 9).Value = a.Estado ? "Activo" : "Inactivo";
            hoja.Cell(fila, 9).Style.Font.FontColor = XLColor.FromHtml(a.Estado ? ColorExito : ColorError);
            fila++;
        }

        AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, columnas.Length);
        hoja.Columns(1, columnas.Length).AdjustToContents();
        hoja.SheetView.FreezeRows(filaEncabezado);
    }

    private void CrearHojaDesviaciones(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("Desviaciones");
        EstilarTitulo(hoja, "Desviaciones de Planificación (CC con cumplimiento < 70%)", d.NombreEmpresa);

        var filaEncabezado = 5;
        hoja.Cell(filaEncabezado, 1).Value = "Centro de Costo";
        hoja.Cell(filaEncabezado, 2).Value = "Cumpl. Demanda %";
        hoja.Cell(filaEncabezado, 3).Value = "Cumpl. Venta %";
        EstilarEncabezado(hoja, filaEncabezado, 3, "#F59E0B");

        var fila = filaEncabezado + 1;
        foreach (var item in d.Desviaciones)
        {
            hoja.Cell(fila, 1).Value = item.CentroCosto;
            hoja.Cell(fila, 2).Value = (double)item.CumplimientoDemanda / 100;
            hoja.Cell(fila, 2).Style.NumberFormat.Format = "0.0%";
            hoja.Cell(fila, 2).Style.Font.FontColor = XLColor.FromHtml(item.CumplimientoDemanda >= 70 ? ColorExito : ColorError);
            hoja.Cell(fila, 3).Value = (double)item.CumplimientoVenta / 100;
            hoja.Cell(fila, 3).Style.NumberFormat.Format = "0.0%";
            hoja.Cell(fila, 3).Style.Font.FontColor = XLColor.FromHtml(item.CumplimientoVenta >= 70 ? ColorExito : ColorError);
            fila++;
        }

        if (fila == filaEncabezado + 1)
        {
            hoja.Cell(fila, 1).Value = "Sin desviaciones detectadas.";
            hoja.Cell(fila, 1).Style.Font.Italic = true;
            hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml(ColorTextoSecundario);
        }
        else
        {
            AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, 3);
        }

        hoja.Columns(1, 3).AdjustToContents();
    }

    private void CrearHojaCrm(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("CRM");
        EstilarTitulo(hoja, "CRM – Clientes sin contacto reciente", d.NombreEmpresa);

        if (d.ResumenCrm is not null)
        {
            hoja.Cell(5, 1).Value = "Clientes nuevos (período)";
            hoja.Cell(5, 1).Style.Font.FontColor = XLColor.FromHtml(ColorTextoSecundario);
            hoja.Cell(5, 2).Value = d.ResumenCrm.ClientesNuevos;
            hoja.Cell(5, 2).Style.Font.Bold = true;

            hoja.Cell(6, 1).Value = "Interacciones (período)";
            hoja.Cell(6, 1).Style.Font.FontColor = XLColor.FromHtml(ColorTextoSecundario);
            hoja.Cell(6, 2).Value = d.ResumenCrm.Interacciones;
            hoja.Cell(6, 2).Style.Font.Bold = true;
        }

        var filaEncabezado = 8;
        hoja.Cell(filaEncabezado, 1).Value = "Cliente";
        hoja.Cell(filaEncabezado, 2).Value = "Responsable";
        hoja.Cell(filaEncabezado, 3).Value = "Última Interacción";
        hoja.Cell(filaEncabezado, 4).Value = "Próximo Contacto";
        EstilarEncabezado(hoja, filaEncabezado, 4, ColorPrimarioOscuro);

        var fila = filaEncabezado + 1;
        foreach (var c in d.ClientesFrios)
        {
            hoja.Cell(fila, 1).Value = c.Nombre;
            hoja.Cell(fila, 2).Value = c.Responsable ?? "-";
            hoja.Cell(fila, 3).Value = c.UltimaInteraccion is not null ? c.UltimaInteraccion.Value.ToString("dd/MM/yyyy") : "Sin registro";
            hoja.Cell(fila, 4).Value = c.ProximoContacto is not null ? c.ProximoContacto.Value.ToString("dd/MM/yyyy") : "-";
            hoja.Cell(fila, 3).Style.Font.FontColor = XLColor.FromHtml(ColorError);
            fila++;
        }

        if (fila > filaEncabezado + 1)
            AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, 4);

        hoja.Columns(1, 4).AdjustToContents();
    }

    private void CrearHojaAlertasProyectos(XLWorkbook libro, DatosReporte d)
    {
        var hoja = libro.Worksheets.Add("Alertas Proyectos");
        EstilarTitulo(hoja, "Proyectos con Alertas", d.NombreEmpresa);

        var filaEncabezado = 5;
        hoja.Cell(filaEncabezado, 1).Value = "Proyecto";
        hoja.Cell(filaEncabezado, 2).Value = "Fecha Fin";
        hoja.Cell(filaEncabezado, 3).Value = "Presupuesto";
        hoja.Cell(filaEncabezado, 4).Value = "Costo Total";
        hoja.Cell(filaEncabezado, 5).Value = "Atrasado";
        hoja.Cell(filaEncabezado, 6).Value = "Sobrecosto";
        EstilarEncabezado(hoja, filaEncabezado, 6, ColorError);

        var fila = filaEncabezado + 1;
        foreach (var p in d.AlertasProyectos)
        {
            hoja.Cell(fila, 1).Value = p.Nombre;
            hoja.Cell(fila, 2).Value = p.FechaFin.HasValue ? p.FechaFin.Value.ToString("dd/MM/yyyy") : "-";
            hoja.Cell(fila, 3).Value = (double)p.Presupuesto;
            hoja.Cell(fila, 3).Style.NumberFormat.Format = "$#,##0";
            hoja.Cell(fila, 4).Value = (double)p.CostoTotal;
            hoja.Cell(fila, 4).Style.NumberFormat.Format = "$#,##0";
            hoja.Cell(fila, 4).Style.Font.FontColor = XLColor.FromHtml(p.ConSobrecosto ? ColorError : ColorExito);
            hoja.Cell(fila, 5).Value = p.Atrasado ? "Sí" : "No";
            hoja.Cell(fila, 5).Style.Font.FontColor = XLColor.FromHtml(p.Atrasado ? ColorError : ColorExito);
            hoja.Cell(fila, 5).Style.Font.Bold = true;
            hoja.Cell(fila, 6).Value = p.ConSobrecosto ? "Sí" : "No";
            hoja.Cell(fila, 6).Style.Font.FontColor = XLColor.FromHtml(p.ConSobrecosto ? ColorError : ColorExito);
            hoja.Cell(fila, 6).Style.Font.Bold = true;
            fila++;
        }

        if (fila == filaEncabezado + 1)
        {
            hoja.Cell(fila, 1).Value = "Sin alertas de proyectos.";
            hoja.Cell(fila, 1).Style.Font.Italic = true;
            hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml(ColorTextoSecundario);
        }
        else
        {
            AplicarBordesYZebra(hoja, filaEncabezado, fila - 1, 6);
        }

        hoja.Columns(1, 6).AdjustToContents();
    }

    private void AplicarBordesYZebra(IXLWorksheet hoja, int filaEncabezado, int ultimaFila, int columnas)
    {
        if (ultimaFila < filaEncabezado + 1)
            return;

        var rango = hoja.Range(filaEncabezado, 1, ultimaFila, columnas);
        rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        rango.Style.Border.OutsideBorderColor = XLColor.FromHtml(ColorBordes);
        rango.Style.Border.InsideBorderColor = XLColor.FromHtml(ColorBordes);

        for (var fila = filaEncabezado + 1; fila <= ultimaFila; fila++)
        {
            if ((fila - filaEncabezado) % 2 == 0)
                hoja.Range(fila, 1, fila, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(ColorFondoClaro);
        }
    }

    // ==================================================================
    // PDF (QuestPDF)
    // ==================================================================

    public async Task<byte[]> GenerarPdfAsync(DateTime desde, DateTime hasta)
    {
        var datos = await RecolectarDatosAsync(desde, hasta);

        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(30);
                pagina.DefaultTextStyle(x => x.FontSize(9).FontColor(ColorTextoOscuro));

                pagina.Header().Element(c => ComponerEncabezado(c, datos.NombreEmpresa, datos.NombrePropietario, desde, hasta));
                pagina.Content().Element(c => ComponerContenido(c, datos));
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(8).FontColor(ColorTextoSecundario);
                    t.Span(" / ").FontSize(8).FontColor(ColorTextoSecundario);
                    t.TotalPages().FontSize(8).FontColor(ColorTextoSecundario);
                });
            });
        });

        return documento.GeneratePdf();
    }

    private void ComponerEncabezado(QuestPDF.Infrastructure.IContainer contenedor, string nombreEmpresa, string? nombrePropietario, DateTime desde, DateTime hasta)
    {
        contenedor.Background(ColorPrimarioOscuro).Padding(20).Row(fila =>
        {
            fila.RelativeItem().Column(col =>
            {
                col.Item().Text(nombreEmpresa).FontSize(18).Bold().FontColor(Colors.White);
                if (!string.IsNullOrEmpty(nombrePropietario))
                    col.Item().Text($"Propietario: {nombrePropietario}").FontSize(9).FontColor("#D8D0FF");
                col.Item().Text("Reporte de Producción e Inventario").FontSize(11).FontColor(Colors.White);
                col.Item().PaddingTop(3).Text($"Período: {desde:dd/MM/yyyy} — {hasta:dd/MM/yyyy}").FontSize(8).FontColor("#D8D0FF");
            });
            fila.ConstantItem(140).AlignRight().AlignMiddle()
                .Text($"Generado\n{NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#D8D0FF").AlignRight();
        });
    }

    private void ComponerContenido(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.PaddingVertical(15).Column(col =>
        {
            col.Spacing(16);

            col.Item().Element(c => ComponerKpis(c, d));
            col.Item().Element(c => ComponerBarrasPlanVsReal(c, d));
            col.Item().Element(c => ComponerTablaDistribucion(c, d));
            col.Item().Element(c => ComponerTablaCumplimiento(c, d));
            col.Item().Element(c => ComponerTablaDesviaciones(c, d));
            col.Item().Element(c => ComponerTablaPerdidas(c, d));
            col.Item().Element(c => ComponerTablaAlertasProyectos(c, d));
            col.Item().Element(c => ComponerTablaCrm(c, d));
            col.Item().PageBreak();
            col.Item().Element(c => ComponerTablaStock(c, d));
        });
    }

    private void ComponerTitulo(QuestPDF.Infrastructure.IContainer contenedor, string texto) =>
        contenedor.Text(texto).FontSize(13).Bold().FontColor(ColorPrimarioOscuro);

    private void ComponerKpis(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        var totalPlanificado = d.PlanVsReal.Sum(p => p.TotalPlanificado);
        var totalReal = d.PlanVsReal.Sum(p => p.TotalReal);
        var cumplimientoGlobal = totalPlanificado > 0 ? totalReal / totalPlanificado * 100 : 0;
        var totalPerdidas = d.Perdidas.Sum(p => p.ValorTotalPerdido);
        var valorStock = d.Stock.Sum(s => s.ValorTotal);

        var kpis = new (string Titulo, string Valor, string Color)[]
        {
            ("Planificado", totalPlanificado.ToString("N0"), ColorInfo),
            ("Real Producido", totalReal.ToString("N0"), ColorPrimario),
            ("Cumplimiento", $"{cumplimientoGlobal:N1}%", cumplimientoGlobal >= 100 ? ColorExito : ColorError),
            ("Pérdidas", totalPerdidas.ToString("C0"), ColorError),
            ("Valor Stock", valorStock.ToString("C0"), ColorExito),
        };

        contenedor.Row(fila =>
        {
            foreach (var kpi in kpis)
            {
                fila.RelativeItem().Border(1).BorderColor(ColorBordes).Background(Colors.White)
                    .Padding(10).Column(col =>
                    {
                        col.Item().Text(kpi.Titulo).FontSize(7.5f).FontColor(ColorTextoSecundario);
                        col.Item().PaddingTop(2).Text(kpi.Valor).FontSize(14).Bold().FontColor(kpi.Color);
                    });
            }
        });
    }

    // Barras estilo Dashboard: verde cuando cumple/supera el plan, rojo cuando
    // queda por debajo, dibujadas con primitivas de QuestPDF (sin depender de
    // renderizar el SVG del navegador).
    private void ComponerBarrasPlanVsReal(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.Column(col =>
        {
            col.Item().Element(c => ComponerTitulo(c, "Variación de Producción vs Plan"));
            col.Item().PaddingBottom(4).Text("Real sobre planificado por día").FontSize(8).FontColor(ColorTextoSecundario);

            if (d.PlanVsReal.Count == 0)
            {
                col.Item().Text("Sin datos en el período seleccionado.").FontColor(ColorTextoSecundario).Italic();
                return;
            }

            var puntos = d.PlanVsReal.Select(p => new
            {
                p.Fecha,
                Variacion = p.TotalPlanificado > 0 ? (p.TotalReal - p.TotalPlanificado) / p.TotalPlanificado * 100 : 0
            }).ToList();

            var maxAbs = Math.Max(puntos.Max(p => Math.Abs(p.Variacion)), 5);

            col.Item().Height(90).Row(fila =>
            {
                foreach (var p in puntos)
                {
                    var alturaPorcentaje = (float)(Math.Abs(p.Variacion) / maxAbs);
                    var color = p.Variacion >= 0 ? ColorExito : ColorError;

                    fila.RelativeItem().AlignBottom().Column(barraCol =>
                    {
                        if (p.Variacion >= 0)
                        {
                            barraCol.Item().Text($"{(p.Variacion >= 0 ? "+" : "")}{p.Variacion:N1}%")
                                .FontSize(6).FontColor(color).AlignCenter();
                            barraCol.Item().AlignCenter().Height(60 * alturaPorcentaje).Width(14).Background(color);
                        }
                        else
                        {
                            barraCol.Item().AlignCenter().Height(60 * alturaPorcentaje).Width(14).Background(color);
                            barraCol.Item().Text($"{p.Variacion:N1}%")
                                .FontSize(6).FontColor(color).AlignCenter();
                        }
                        barraCol.Item().PaddingTop(2).Text(p.Fecha.ToString("dd/MM")).FontSize(6).FontColor(ColorTextoSecundario).AlignCenter();
                    });
                }
            });
        });
    }

    private void ComponerTablaDistribucion(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.Column(col =>
        {
            col.Item().Element(c => ComponerTitulo(c, "Distribución de Producción por Centro de Costo"));
            col.Item().PaddingTop(6).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(1);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.5f);
                });

                EncabezadoTabla(tabla, null, "Centro de Costo", "Órdenes", "Unidades", "Inversión");

                foreach (var item in d.Distribucion)
                {
                    CeldaTabla(tabla, item.CentroCosto);
                    CeldaTabla(tabla, item.TotalOrdenes.ToString(), alinearDerecha: true);
                    CeldaTabla(tabla, item.TotalUnidadesProducidas.ToString("N0"), alinearDerecha: true);
                    CeldaTabla(tabla, item.InversionTotal.ToString("C0"), alinearDerecha: true);
                }
            });
        });
    }

    private void ComponerTablaCumplimiento(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.Column(col =>
        {
            col.Item().Element(c => ComponerTitulo(c, "Cumplimiento de Planificación"));
            col.Item().PaddingTop(6).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.5f);
                });

                EncabezadoTabla(tabla, null, "Centro de Costo", "Finalizadas", "A Tiempo", "% Cumplimiento");

                foreach (var item in d.Cumplimiento)
                {
                    var color = item.PorcentajeCumplimiento >= 90 ? ColorExito : item.PorcentajeCumplimiento >= 70 ? "#F59E0B" : ColorError;
                    CeldaTabla(tabla, item.CentroCosto);
                    CeldaTabla(tabla, item.TotalOrdenesFinalizadas.ToString(), alinearDerecha: true);
                    CeldaTabla(tabla, item.OrdenesATiempo.ToString(), alinearDerecha: true);
                    tabla.Cell().Element(CeldaBase).AlignRight().Text($"{item.PorcentajeCumplimiento:N1}%").Bold().FontColor(color);
                }
            });
        });
    }

    private void ComponerTablaPerdidas(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.Column(col =>
        {
            col.Item().Element(c => ComponerTitulo(c, "Pérdidas de Inventario por Motivo"));

            if (d.Perdidas.Count == 0)
            {
                col.Item().PaddingTop(4).Text("Sin pérdidas registradas en el período.").FontColor(ColorTextoSecundario).Italic();
                return;
            }

            col.Item().PaddingTop(6).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });

                EncabezadoTabla(tabla, ColorError, "Motivo", "Cantidad Perdida", "Valor Perdido");

                foreach (var item in d.Perdidas)
                {
                    CeldaTabla(tabla, item.Motivo);
                    CeldaTabla(tabla, item.CantidadTotalPerdida.ToString("N2"), alinearDerecha: true);
                    CeldaTabla(tabla, item.ValorTotalPerdido.ToString("C0"), alinearDerecha: true);
                }
            });
        });
    }

    private void ComponerTablaStock(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.Column(col =>
        {
            col.Item().Element(c => ComponerTitulo(c, "Stock Consolidado Actual"));
            col.Item().PaddingTop(6).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.3f);
                    c.RelativeColumn(2.5f);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.3f);
                    c.RelativeColumn(1.5f);
                });

                EncabezadoTabla(tabla, null, "SKU", "Artículo", "Bodega", "Cantidad", "Valor Total");

                foreach (var s in d.Stock)
                {
                    CeldaTabla(tabla, s.SKU);
                    CeldaTabla(tabla, s.Articulo);
                    CeldaTabla(tabla, s.Bodega);
                    CeldaTabla(tabla, $"{s.CantidadActual:N2} {s.Unidad}", alinearDerecha: true);
                    CeldaTabla(tabla, s.ValorTotal.ToString("C0"), alinearDerecha: true);
                }
            });
        });
    }

    private void ComponerTablaDesviaciones(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.Column(col =>
        {
            col.Item().Element(c => ComponerTitulo(c, "Desviaciones de Planificación (< 70%)"));
            col.Item().PaddingTop(6).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });

                EncabezadoTabla(tabla, "#F59E0B", "Centro de Costo", "Cumpl. Demanda", "Cumpl. Venta");

                foreach (var item in d.Desviaciones)
                {
                    CeldaTabla(tabla, item.CentroCosto);
                    var colorD = item.CumplimientoDemanda >= 70 ? ColorExito : ColorError;
                    var colorV = item.CumplimientoVenta >= 70 ? ColorExito : ColorError;
                    tabla.Cell().Element(CeldaBase).AlignRight().Text($"{item.CumplimientoDemanda:N1}%").FontColor(colorD).Bold();
                    tabla.Cell().Element(CeldaBase).AlignRight().Text($"{item.CumplimientoVenta:N1}%").FontColor(colorV).Bold();
                }
            });
        });
    }

    private void ComponerTablaAlertasProyectos(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.Column(col =>
        {
            col.Item().Element(c => ComponerTitulo(c, "Proyectos con Alertas"));
            if (d.AlertasProyectos.Count == 0)
            {
                col.Item().PaddingTop(4).Text("Sin alertas de proyectos en este período.").FontColor(ColorTextoSecundario).Italic();
                return;
            }
            col.Item().PaddingTop(6).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1);
                    c.RelativeColumn(1);
                });

                EncabezadoTabla(tabla, ColorError, "Proyecto", "Presupuesto", "Costo Real", "Atrasado", "Sobrecosto");

                foreach (var p in d.AlertasProyectos)
                {
                    CeldaTabla(tabla, p.Nombre);
                    CeldaTabla(tabla, p.Presupuesto.ToString("C0"), alinearDerecha: true);
                    tabla.Cell().Element(CeldaBase).AlignRight()
                        .Text(p.CostoTotal.ToString("C0")).FontColor(p.ConSobrecosto ? ColorError : ColorExito);
                    tabla.Cell().Element(CeldaBase).AlignCenter()
                        .Text(p.Atrasado ? "Sí" : "No").Bold().FontColor(p.Atrasado ? ColorError : ColorExito);
                    tabla.Cell().Element(CeldaBase).AlignCenter()
                        .Text(p.ConSobrecosto ? "Sí" : "No").Bold().FontColor(p.ConSobrecosto ? ColorError : ColorExito);
                }
            });
        });
    }

    private void ComponerTablaCrm(QuestPDF.Infrastructure.IContainer contenedor, DatosReporte d)
    {
        contenedor.Column(col =>
        {
            col.Item().Element(c => ComponerTitulo(c, $"CRM – Clientes sin contacto (+30 días): {d.ClientesFrios.Count}"));
            if (d.ClientesFrios.Count == 0)
            {
                col.Item().PaddingTop(4).Text("Sin clientes inactivos. Todos con contacto reciente.").FontColor(ColorTextoSecundario).Italic();
                return;
            }
            col.Item().PaddingTop(6).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.5f);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1.5f);
                    c.RelativeColumn(1.5f);
                });

                EncabezadoTabla(tabla, null, "Cliente", "Responsable", "Última Interacción", "Próx. Contacto");

                foreach (var c in d.ClientesFrios)
                {
                    CeldaTabla(tabla, c.Nombre);
                    CeldaTabla(tabla, c.Responsable ?? "-");
                    tabla.Cell().Element(CeldaBase)
                        .Text(c.UltimaInteraccion.HasValue ? c.UltimaInteraccion.Value.ToString("dd/MM/yyyy") : "Sin registro")
                        .FontColor(ColorError).FontSize(8);
                    CeldaTabla(tabla, c.ProximoContacto.HasValue ? c.ProximoContacto.Value.ToString("dd/MM/yyyy") : "-");
                }
            });
        });
    }

    private void EncabezadoTabla(TableDescriptor tabla, string? colorFondo = null, params string[] titulos)
    {
        var color = colorFondo ?? ColorPrimarioOscuro;

        foreach (var titulo in titulos)
        {
            tabla.Cell().Background(color).Padding(5).Text(titulo).FontColor(Colors.White).Bold().FontSize(8);
        }
    }

    private static QuestPDF.Infrastructure.IContainer CeldaBase(QuestPDF.Infrastructure.IContainer contenedor) =>
        contenedor.BorderBottom(1).BorderColor("#E8E9EF").PaddingVertical(4).PaddingHorizontal(5);

    private void CeldaTabla(TableDescriptor tabla, string texto, bool alinearDerecha = false)
    {
        var celda = tabla.Cell().Element(CeldaBase);
        if (alinearDerecha)
            celda.AlignRight().Text(texto).FontSize(8);
        else
            celda.Text(texto).FontSize(8);
    }
}
