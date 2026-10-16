// Features/Inventario/InventarioController.cs
using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Catalogo;
using NexoApi.Features.Inventario.Dtos;

namespace NexoApi.Features.Inventario;

[ApiController]
[Route("api/inventario")]
[Authorize]
public class InventarioController : ControllerBase
{
    private readonly IInventarioService _service;
    private readonly ICatalogoService _catalogo;

    public InventarioController(IInventarioService service, ICatalogoService catalogo)
    {
        _service  = service;
        _catalogo = catalogo;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Consulta el stock consolidado, con filtros opcionales.</summary>
    [HttpGet("stock")]
    public async Task<ActionResult<IEnumerable<StockConsolidadoItem>>> ConsultarStock(
        [FromQuery] int? centroCostoId, [FromQuery] int? bodegaId,
        [FromQuery] string? filtro, [FromQuery] int? articuloId)
    {
        var stock = await _service.ConsultarStockAsync(centroCostoId, bodegaId, filtro, articuloId);
        return Ok(stock);
    }

    /// <summary>Registra una baja por dano o merma accidental. Descuenta stock y genera KARDEX.</summary>
    [HttpPost("bajas")]
    [Authorize]
    public async Task<ActionResult<RegistrarBajaResponse>> RegistrarBaja(RegistrarBajaRequest request)
    {
        var resultado = await _service.RegistrarBajaAsync(request, UsuarioActualId);
        return Ok(resultado);
    }

    /// <summary>Registra una entrada positiva de inventario (carga inicial o correccion por conteo fisico). Aumenta stock y genera KARDEX.</summary>
    [HttpPost("ajustes")]
    [Authorize]
    public async Task<ActionResult<AjustarInventarioResponse>> AjustarInventario(AjustarInventarioRequest request)
    {
        var resultado = await _service.AjustarInventarioAsync(request, UsuarioActualId);
        return Ok(resultado);
    }

    [HttpGet("motivos-perdida")]
    public async Task<ActionResult<IEnumerable<MotivoPerdidaItem>>> ListarMotivosPerdida()
        => Ok(await _service.ListarMotivosPerdidaAsync());

    [HttpGet("kardex")]
    public async Task<ActionResult<IEnumerable<KardexMovimientoItem>>> ConsultarKardex(
        [FromQuery] int? articuloId, [FromQuery] int? bodegaId,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        => Ok(await _service.ConsultarKardexAsync(articuloId, bodegaId, desde, hasta));

    [HttpGet("lotes-por-vencer")]
    public async Task<ActionResult<IEnumerable<LoteProximoVencerItem>>> ListarLotesPorVencer(
        [FromQuery] int diasAlerta = 30)
        => Ok(await _service.ListarLotesPorVencerAsync(diasAlerta));

    [HttpGet("oc-sugerida")]
    public async Task<ActionResult<IEnumerable<OCLineSugeridaItem>>> SugerirOC()
        => Ok(await _service.SugerirOCAsync());

    [HttpGet("ajustes/plantilla")]
    public IActionResult DescargarPlantillaAjustes()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Ajustes");
        ws.Cell(1, 1).Value = "Referencia";
        ws.Cell(1, 2).Value = "Cantidad";
        ws.Cell(1, 3).Value = "Costo";
        ws.Cell(1, 4).Value = "Motivo";
        var hdr = ws.Range("A1:D1");
        hdr.Style.Font.Bold = true;
        hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1976D2");
        hdr.Style.Font.FontColor = XLColor.White;
        ws.Cell(2, 1).Value = "REF001";
        ws.Cell(2, 2).Value = 10;
        ws.Cell(2, 3).Value = 5000;
        ws.Cell(2, 4).Value = "Carga inicial de stock";
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "plantilla_ajustes_inventario.xlsx");
    }

    [HttpPost("ajustes/importar-excel")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ImportarAjustesResult>> ImportarAjustesExcel(
        IFormFile archivo, [FromQuery] int bodegaId)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "Archivo vacío o no enviado." });
        if (bodegaId <= 0)
            return BadRequest(new { error = "Debes seleccionar una bodega." });

        int procesados = 0, errores = 0;
        var mensajes = new List<string>();

        using var stream = archivo.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in ws.Row(1).CellsUsed())
            headers[cell.GetString().Trim()] = cell.Address.ColumnNumber;

        string? Col(IXLRow row, string name)
        {
            if (!headers.TryGetValue(name, out var c)) return null;
            var v = row.Cell(c).GetString().Trim();
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }
        decimal? Dec(IXLRow row, string name)
        {
            var s = Col(row, name);
            return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        for (int r = 2; r <= ws.LastRowUsed()?.RowNumber(); r++)
        {
            var row = ws.Row(r);
            var ref_ = Col(row, "Referencia");
            if (string.IsNullOrWhiteSpace(ref_)) continue;

            try
            {
                var coincidencias = await _catalogo.BuscarArticulosAsync(ref_, null, 1);
                var articulo = coincidencias.FirstOrDefault(a =>
                    a.Referencia.Equals(ref_, StringComparison.OrdinalIgnoreCase));

                if (articulo is null)
                {
                    errores++;
                    mensajes.Add($"Fila {r}: Referencia '{ref_}' no encontrada.");
                    continue;
                }

                var cantidad = Dec(row, "Cantidad");
                if (cantidad is null or <= 0)
                {
                    errores++;
                    mensajes.Add($"Fila {r} ({ref_}): Cantidad inválida o vacía.");
                    continue;
                }

                var costo  = Dec(row, "Costo") ?? 0m;
                var motivo = Col(row, "Motivo") ?? "Importación masiva de inventario";

                var req = new AjustarInventarioRequest(
                    articulo.ArticuloID, bodegaId, cantidad.Value, costo, motivo);
                await _service.AjustarInventarioAsync(req, UsuarioActualId);
                procesados++;
            }
            catch (Exception ex)
            {
                errores++;
                mensajes.Add($"Fila {r} ({ref_}): {ex.Message}");
            }
        }

        return Ok(new ImportarAjustesResult(procesados, errores, mensajes));
    }

    [HttpGet("mermas")]
    [Authorize]
    public async Task<ActionResult<ResumenMermasResponse>> ListarMermas(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? tipo)
        => Ok(await _service.ListarMermasAsync(desde, hasta, tipo));
}