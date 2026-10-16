using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Export;
using NexoApi.Features.Catalogo.Dtos;
using NexoApi.Features.Configuracion;
using NexoApi.Common.Security;

namespace NexoApi.Features.Catalogo;

[ApiController]
[Route("api/catalogo/articulos")]
[Authorize]
public class ArticulosController : ControllerBase
{
    private readonly ICatalogoService _service;
    private readonly IConfiguracionService _config;
    private readonly IAdicionalesService _adicionales;

    public ArticulosController(ICatalogoService service, IConfiguracionService config, IAdicionalesService adicionales)
    {
        _service    = service;
        _config     = config;
        _adicionales = adicionales;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string NombreUsuarioActual =>
        User.FindFirstValue(ClaimTypes.Name) ?? "Desconocido";

    [HttpGet]
    public async Task<ActionResult<ArticulosPaginadosResponse>> Listar(
        [FromQuery] int? tipoArticuloId, [FromQuery] string? texto,
        [FromQuery] bool? estado,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 100,
        [FromQuery] int? centroCostoId = null)
    {
        return Ok(await _service.ListarArticulosAsync(tipoArticuloId, texto, estado, pagina, tamano, centroCostoId));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ArticuloItem>> ObtenerPorId(int id)
    {
        var articulo = await _service.ObtenerArticuloAsync(id);
        return articulo is null ? NotFound() : Ok(articulo);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> Crear(CrearArticuloRequest request)
    {
        try
        {
            var id = await _service.CrearArticuloAsync(request);
            // El body devuelve el ID (no solo el header Location) para que el
            // frontend pueda encadenar la subida de imagen opcional justo despues.
            return CreatedAtAction(nameof(Listar), new { ArticuloID = id }, new { articuloId = id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<ActionResult> Actualizar(int id, ActualizarArticuloRequest request)
    {
        try
        {
            await _service.ActualizarArticuloAsync(id, request, UsuarioActualId, NombreUsuarioActual);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("todos")]
    public async Task<ActionResult<IEnumerable<ArticuloItem>>> ListarTodos(
        [FromQuery] int? tipoArticuloId, [FromQuery] bool? estado)
    {
        return Ok(await _service.ListarTodosArticulosAsync(tipoArticuloId, estado));
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> Contar([FromQuery] bool? estado)
        => Ok(await _service.ContarArticulosAsync(estado));

    [HttpGet("buscar")]
    public async Task<ActionResult<IEnumerable<ArticuloItem>>> Buscar(
        [FromQuery] string? q, [FromQuery] bool? soloTerminados, [FromQuery] int max = 30)
    {
        return Ok(await _service.BuscarArticulosAsync(q, soloTerminados, max));
    }

    [HttpGet("tipos")]
    public async Task<ActionResult<IEnumerable<TipoArticuloItem>>> ListarTipos()
    {
        return Ok(await _service.ListarTiposArticuloAsync());
    }

    [HttpGet("unidades")]
    public async Task<ActionResult<IEnumerable<UnidadMedidaItem>>> ListarUnidades()
    {
        return Ok(await _service.ListarUnidadesMedidaAsync());
    }

    // ================= Imagen (opcional, una sola por articulo) =================

    // Sin [Authorize]: un <img src="..."> plano no puede mandar el header
    // Authorization, asi que este endpoint especifico queda anonimo a
    // proposito (solo sirve bytes de una foto de producto por ID, no es
    // informacion sensible). El resto de la API sigue exigiendo login.
    [HttpGet("{id:int}/imagen")]
    [AllowAnonymous]
    public async Task<IActionResult> ObtenerImagen(int id)
    {
        var imagen = await _service.ObtenerImagenArticuloAsync(id);
        if (imagen is null)
            return NotFound();

        return File(imagen.Value.Datos, imagen.Value.ContentType);
    }

    [HttpPut("{id:int}/imagen")]
    [Authorize]
    public async Task<ActionResult> ActualizarImagen(int id, ActualizarImagenRequest request)
    {
        if (Convert.FromBase64String(request.Base64).Length > 1_000_000)
            return BadRequest(new { error = "La imagen es muy grande (m�ximo 1 MB)." });

        try
        {
            await _service.ActualizarImagenArticuloAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}/imagen")]
    [Authorize]
    public async Task<ActionResult> EliminarImagen(int id)
    {
        try
        {
            await _service.EliminarImagenArticuloAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── Historial de precios ─────────────────────────────────────────────────
    [HttpGet("{id:int}/historial-precios")]
    public async Task<ActionResult<IEnumerable<HistorialPrecioItem>>> ListarHistorial(int id)
        => Ok(await _service.ListarHistorialPreciosAsync(id));

    // ── Exportar a Excel ─────────────────────────────────────────────────────
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar()
    {
        var articulos = await _service.ListarTodosArticulosAsync();
        var bytes = ExportService.GenerarExcelArticulos(articulos);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"articulos_{NexoApi.Common.ColombiaClock.Now:yyyyMMdd}.xlsx");
    }

    // ── Ficha técnica PDF ────────────────────────────────────────────────────

    [HttpGet("{id:int}/ficha-pdf")]
    public async Task<IActionResult> FichaTecnica(int id)
    {
        var articulo = await _service.ObtenerArticuloAsync(id);
        if (articulo is null) return NotFound();
        var imagen  = await _service.ObtenerImagenArticuloAsync(id);
        var empresa = await _config.ObtenerEmpresaAsync();
        var bytes   = ExportService.GenerarFichaTecnicaPdf(articulo,
            imagen?.Datos, imagen?.ContentType, empresa.NombreEmpresa);
        return File(bytes, "application/pdf", $"ficha_{articulo.Referencia}.pdf");
    }

    // ── Plantilla de importación ─────────────────────────────────────────────

    [HttpGet("plantilla")]
    public IActionResult DescargarPlantilla()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Artículos");
        string[] cols = { "Referencia", "Nombre", "Descripcion", "Costo", "PPublico", "PBodega",
                          "StockMinimo", "PuntoReorden", "MarcaCodigo", "GrupoMenorCodigo", "PresentacionCodigo" };
        for (int i = 0; i < cols.Length; i++) ws.Cell(1, i + 1).Value = cols[i];
        var hdr = ws.Range(1, 1, 1, cols.Length);
        hdr.Style.Font.Bold = true;
        hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1976D2");
        hdr.Style.Font.FontColor = XLColor.White;
        ws.Cell(2, 1).Value = "REF001";
        ws.Cell(2, 2).Value = "Nombre de ejemplo";
        ws.Cell(2, 4).Value = 8000;
        ws.Cell(2, 5).Value = 12000;
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "plantilla_articulos.xlsx");
    }

    // ── Importación masiva por Excel ──────────────────────────────────────────

    [HttpPost("importar-excel")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ImportarExcelResult>> ImportarExcel(IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "Archivo vacío o no enviado." });

        int creados = 0, actualizados = 0, errores = 0;
        var mensajes = new List<string>();

        using var stream = archivo.OpenReadStream();
        using var wb     = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        // Detectar encabezados en fila 1
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
            return decimal.TryParse(s, out var d) ? d : null;
        }

        for (int r = 2; r <= ws.LastRowUsed()?.RowNumber(); r++)
        {
            var row  = ws.Row(r);
            var ref_ = Col(row, "Referencia");
            if (string.IsNullOrWhiteSpace(ref_)) continue;

            try
            {
                var existente = await _service.BuscarArticulosAsync(ref_, null, 1);
                var articulo  = existente.FirstOrDefault(a => a.Referencia.Equals(ref_, StringComparison.OrdinalIgnoreCase));

                if (articulo is null)
                {
                    var nombre = Col(row, "Nombre") ?? ref_;
                    var req = new CrearArticuloRequest(
                        Referencia: ref_, Nombre: nombre,
                        Descripcion: Col(row, "Descripcion"),
                        TipoArticuloID: 1,
                        StockMinimo: Dec(row, "StockMinimo") ?? 0,
                        PuntoReorden: Dec(row, "PuntoReorden") ?? 0,
                        DiasVidaUtil: null,
                        Costo: Dec(row, "Costo"),
                        PPublico: Dec(row, "PPublico"),
                        PBodega: Dec(row, "PBodega"),
                        PCredito: Dec(row, "PCredito"),
                        MarcaCodigo: Col(row, "MarcaCodigo"),
                        GrupoMenorCodigo: Col(row, "GrupoMenorCodigo"),
                        PresentacionCodigo: Col(row, "PresentacionCodigo")
                    );
                    await _service.CrearArticuloAsync(req);
                    creados++;
                }
                else
                {
                    var req = new ActualizarArticuloRequest(
                        Nombre: Col(row, "Nombre") ?? articulo.Nombre,
                        Descripcion: Col(row, "Descripcion") ?? articulo.Descripcion,
                        StockMinimo: Dec(row, "StockMinimo") ?? articulo.StockMinimo,
                        PuntoReorden: Dec(row, "PuntoReorden") ?? articulo.PuntoReorden,
                        DiasVidaUtil: articulo.DiasVidaUtil,
                        Estado: articulo.Estado,
                        Costo: Dec(row, "Costo") ?? articulo.Costo,
                        PPublico: Dec(row, "PPublico") ?? articulo.PPublico,
                        PBodega: Dec(row, "PBodega") ?? articulo.PBodega,
                        PCredito: Dec(row, "PCredito") ?? articulo.PCredito,
                        MarcaCodigo: Col(row, "MarcaCodigo") ?? articulo.MarcaCodigo,
                        GrupoMenorCodigo: Col(row, "GrupoMenorCodigo") ?? articulo.GrupoMenorCodigo,
                        PresentacionCodigo: Col(row, "PresentacionCodigo") ?? articulo.PresentacionCodigo,
                        TipoArticuloId: articulo.TipoArticuloID
                    );
                    await _service.ActualizarArticuloAsync(articulo.ArticuloID, req,
                        UsuarioActualId, NombreUsuarioActual);
                    actualizados++;
                }
            }
            catch (Exception ex)
            {
                errores++;
                mensajes.Add($"Fila {r} ({ref_}): {ex.Message}");
            }
        }

        return Ok(new ImportarExcelResult(creados, actualizados, errores, mensajes));
    }

    // ── Adicionales (toppings/extras) ────────────────────────────────────────

    [HttpGet("{id:int}/es-adicional")]
    public async Task<ActionResult<bool>> EsAdicional(int id)
        => Ok(await _adicionales.EsAdicionalAsync(id));

    [HttpPost("{id:int}/es-adicional")]
    public async Task<ActionResult> MarcarEsAdicional(int id, MarcarEsAdicionalRequest request)
    {
        await _adicionales.MarcarEsAdicionalAsync(id, request.EsAdicional);
        return NoContent();
    }

    [HttpGet("{id:int}/adicionales")]
    public async Task<ActionResult<IEnumerable<ArticuloAdicionalItem>>> ListarAdicionales(int id)
        => Ok(await _adicionales.ListarAsignadosAsync(id));

    [HttpGet("{id:int}/adicionales/disponibles")]
    public async Task<ActionResult<IEnumerable<ArticuloAdicionalItem>>> ListarAdicionalesDisponibles(int id)
        => Ok(await _adicionales.ListarDisponiblesAsync(id));

    [HttpPost("{id:int}/adicionales")]
    public async Task<ActionResult> AgregarAdicional(int id, AgregarAdicionalRequest request)
    {
        await _adicionales.AgregarAsync(id, request.AdicionalID);
        return NoContent();
    }

    [HttpDelete("{id:int}/adicionales/{adicionalId:int}")]
    public async Task<ActionResult> QuitarAdicional(int id, int adicionalId)
    {
        await _adicionales.QuitarAsync(id, adicionalId);
        return NoContent();
    }

    [HttpGet("adicionales/sync")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<AdicionalesSyncResponse>> SyncData()
        => Ok(await _adicionales.ObtenerDatosSyncAsync());

    // El agente llama aquí para subir lo que Visions tiene (upsert; no elimina en NEXO).
    [HttpPost("adicionales/sync-desde-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> SyncDesdeVisions([FromBody] AdicionalesSyncDesdeVisionsRequest request)
    {
        await _adicionales.SincronizarDesdeVisionsAsync(request);
        return NoContent();
    }

    // ── Variantes ────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/variantes")]
    public async Task<ActionResult<IEnumerable<VarianteItem>>> ListarVariantes(int id)
        => Ok(await _service.ListarVariantesAsync(id));

    [HttpPost("{id:int}/variantes")]
    public async Task<ActionResult> CrearVariante(int id, CrearVarianteRequest request)
    {
        try
        {
            var varianteId = await _service.CrearVarianteAsync(id, request);
            return Ok(new { varianteId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}