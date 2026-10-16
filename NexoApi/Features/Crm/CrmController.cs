using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Export;
using NexoApi.Features.Configuracion;
using NexoApi.Features.Crm.Dtos;

namespace NexoApi.Features.Crm;

[ApiController]
[Route("api/crm")]
[Authorize]
public class CrmController : ControllerBase
{
    private readonly ICrmService _service;
    private readonly IConfiguracionService _config;

    public CrmController(ICrmService service, IConfiguracionService config)
    {
        _service = service;
        _config  = config;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("clientes")]
    public async Task<ActionResult<IEnumerable<ClienteItem>>> ListarClientes(
        [FromQuery] int? responsableId, [FromQuery] string? tipoCliente,
        [FromQuery] string? fuenteContacto, [FromQuery] bool? soloActivos)
        => Ok(await _service.ListarClientesAsync(responsableId, tipoCliente, fuenteContacto, soloActivos));

    [HttpGet("clientes/paginados")]
    public async Task<ActionResult<ClientesPaginadosResponse>> ListarClientesPaginados(
        [FromQuery] string? texto, [FromQuery] int? responsableId,
        [FromQuery] string? tipoCliente, [FromQuery] string? fuenteContacto,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 100)
        => Ok(await _service.ListarClientesPaginadosAsync(texto, responsableId, tipoCliente, fuenteContacto, pagina, tamano));

    // Usa ExternalId (string opaco) para que la URL no exponga el int primario.
    // El workspace carga el cliente por aqui y luego usa ClienteID (int) internamente.
    [HttpGet("clientes/{externalId}")]
    public async Task<ActionResult<ClienteItem>> ObtenerCliente(string externalId)
    {
        var cliente = await _service.ObtenerClienteAsync(externalId);
        return cliente is null ? NotFound() : Ok(cliente);
    }

    [HttpPost("clientes")]
    public async Task<ActionResult> CrearCliente(CrearClienteRequest request)
    {
        var id = await _service.CrearClienteAsync(request);
        return CreatedAtAction(nameof(ListarClientes), new { }, new { clienteId = id });
    }

    [HttpPut("clientes/{id:int}")]
    public async Task<ActionResult> ActualizarCliente(int id, ActualizarClienteRequest request)
    {
        try
        {
            await _service.ActualizarClienteAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("paises")]
    public async Task<ActionResult<IEnumerable<PaisItem>>> ListarPaises()
        => Ok(await _service.ListarPaisesAsync());

    [HttpGet("municipios")]
    public async Task<ActionResult<IEnumerable<MunicipioItem>>> ListarMunicipios()
        => Ok(await _service.ListarMunicipiosAsync());

    [HttpGet("tipos-identificacion")]
    public async Task<ActionResult<IEnumerable<TipoIdentificacionItem>>> ListarTiposIdentificacion()
        => Ok(await _service.ListarTiposIdentificacionAsync());

    [HttpGet("clientes/{id:int}/interacciones")]
    public async Task<ActionResult<IEnumerable<InteraccionItem>>> ListarInteracciones(int id)
        => Ok(await _service.ListarInteraccionesAsync(id));

    [HttpPost("interacciones")]
    public async Task<ActionResult> CrearInteraccion(CrearInteraccionRequest request)
    {
        var id = await _service.CrearInteraccionAsync(request, UsuarioActualId);
        return Ok(new { interaccionId = id });
    }

    // ---------- A) Contactos ----------

    [HttpGet("clientes/{id:int}/contactos")]
    public async Task<ActionResult<IEnumerable<ContactoItem>>> ListarContactos(int id)
        => Ok(await _service.ListarContactosAsync(id));

    [HttpPost("contactos")]
    public async Task<ActionResult> CrearContacto(CrearContactoRequest request)
    {
        var id = await _service.CrearContactoAsync(request);
        return Ok(new { contactoId = id });
    }

    [HttpPut("contactos/{id:int}")]
    public async Task<ActionResult> ActualizarContacto(int id, ActualizarContactoRequest request)
    {
        try
        {
            await _service.ActualizarContactoAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- C) Historial unificado ----------

    [HttpGet("clientes/{id:int}/historial")]
    public async Task<ActionResult<IEnumerable<EventoHistorialItem>>> ObtenerHistorial(int id)
        => Ok(await _service.ObtenerHistorialAsync(id));

    // ---------- E) Documentos ----------

    [HttpGet("clientes/{id:int}/documentos")]
    public async Task<ActionResult<IEnumerable<ClienteDocumentoItem>>> ListarDocumentos(int id)
        => Ok(await _service.ListarDocumentosAsync(id));

    [HttpPost("clientes/{id:int}/documentos")]
    public async Task<ActionResult> SubirDocumento(int id, SubirDocumentoRequest request)
    {
        if (Convert.FromBase64String(request.Base64).Length > 5_000_000)
            return BadRequest(new { error = "El documento es muy grande (máximo 5 MB)." });

        var documentoId = await _service.SubirDocumentoAsync(id, request, UsuarioActualId);
        return Ok(new { documentoId });
    }

    [HttpGet("documentos/{id:int}")]
    public async Task<IActionResult> DescargarDocumento(int id)
    {
        var documento = await _service.ObtenerDocumentoAsync(id);
        if (documento is null)
            return NotFound();

        return File(documento.Value.Datos, documento.Value.ContentType, documento.Value.NombreArchivo);
    }

    [HttpDelete("documentos/{id:int}")]
    public async Task<ActionResult> EliminarDocumento(int id)
    {
        try
        {
            await _service.EliminarDocumentoAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Leads ----------

    [HttpGet("leads")]
    public async Task<ActionResult<IEnumerable<LeadItem>>> ListarLeads([FromQuery] string? etapa, [FromQuery] int? responsableId)
        => Ok(await _service.ListarLeadsAsync(etapa, responsableId));

    [HttpGet("leads/{id:int}")]
    public async Task<ActionResult<LeadItem>> ObtenerLead(int id)
    {
        var lead = await _service.ObtenerLeadAsync(id);
        if (lead is null) return NotFound(new { error = "Lead no encontrado" });
        return Ok(lead);
    }

    [HttpPost("leads")]
    public async Task<ActionResult> CrearLead(CrearLeadRequest request)
    {
        var id = await _service.CrearLeadAsync(request);
        return Ok(new { leadId = id });
    }

    [HttpPut("leads/{id:int}")]
    public async Task<ActionResult> ActualizarLead(int id, ActualizarLeadRequest request)
    {
        try
        {
            await _service.ActualizarLeadAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPatch("leads/{id:int}/etapa")]
    public async Task<ActionResult> CambiarEtapaLead(int id, CambiarEtapaLeadRequest request)
    {
        try
        {
            await _service.CambiarEtapaLeadAsync(id, request.Etapa);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("leads/{id:int}/convertir")]
    public async Task<ActionResult<ConvertirLeadResponse>> ConvertirLead(int id)
    {
        try
        {
            var clienteId = await _service.ConvertirLeadAsync(id);
            return Ok(new ConvertirLeadResponse(clienteId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("leads/{id:int}/notas")]
    public async Task<ActionResult<IEnumerable<NotaLeadItem>>> ListarNotasLead(int id)
        => Ok(await _service.ListarNotasLeadAsync(id));

    [HttpGet("clientes/{id:int}/notas-lead")]
    public async Task<ActionResult<IEnumerable<NotaLeadItem>>> NotasLeadDeCliente(int id)
        => Ok(await _service.ListarNotasLeadPorClienteAsync(id));

    [HttpPost("leads/{id:int}/notas")]
    public async Task<ActionResult> CrearNotaLead(int id, CrearNotaLeadRequest request)
    {
        var notaId = await _service.CrearNotaLeadAsync(id, request, UsuarioActualId);
        return CreatedAtAction(nameof(ListarNotasLead), new { id }, new { notaId });
    }

    // ---------- D) Clientes fríos ----------

    [HttpGet("clientes-frios")]
    public async Task<ActionResult<IEnumerable<ClienteFrioItem>>> ListarClientesFrios([FromQuery] int diasSinContacto = 30)
        => Ok(await _service.ListarClientesFriosAsync(diasSinContacto));

    // ---------- Oportunidades (embudo de ventas, agosto 2026) ----------

    // ---------- Pipeline unificado ----------

    [HttpGet("pipeline")]
    public async Task<ActionResult<IEnumerable<PipelineItem>>> ListarPipeline(
        [FromQuery] string? etapa, [FromQuery] int? responsableId)
        => Ok(await _service.ListarPipelineAsync(etapa, responsableId));

    [HttpGet("pipeline/metricas")]
    public async Task<ActionResult<PipelineMetricasDto>> ObtenerMetricasPipeline()
        => Ok(await _service.ObtenerMetricasPipelineAsync());

    [HttpPost("pipeline/lead/{leadId:int}/oportunidad")]
    public async Task<ActionResult> CrearOportunidadDesdeLead(int leadId, CrearOportunidadDesdeLeadRequest request)
    {
        try
        {
            var id = await _service.CrearOportunidadDesdeLeadAsync(leadId, request);
            return Ok(new { oportunidadId = id });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("oportunidades")]
    public async Task<ActionResult<IEnumerable<OportunidadItem>>> ListarOportunidades([FromQuery] string? etapa, [FromQuery] int? responsableId, [FromQuery] int? leadId)
        => Ok(await _service.ListarOportunidadesAsync(etapa, responsableId, leadId));

    [HttpPost("oportunidades")]
    public async Task<ActionResult> CrearOportunidad(CrearOportunidadRequest request)
    {
        try
        {
            var id = await _service.CrearOportunidadAsync(request);
            return Ok(new { oportunidadId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("oportunidades/{id:int}")]
    public async Task<ActionResult> ActualizarOportunidad(int id, ActualizarOportunidadRequest request)
    {
        try
        {
            await _service.ActualizarOportunidadAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Actividades CRM (agosto 2026) ----------

    [HttpGet("actividades")]
    public async Task<ActionResult<IEnumerable<ActividadItem>>> ListarActividades(
        [FromQuery] bool soloActivas = true,
        [FromQuery] int? oportunidadId = null,
        [FromQuery] int? clienteId = null)
        => Ok(await _service.ListarActividadesAsync(soloActivas, oportunidadId, clienteId));

    [HttpPost("actividades")]
    public async Task<ActionResult<int>> CrearActividad(CrearActividadRequest request)
    {
        try
        {
            var id = await _service.CrearActividadAsync(request);
            return Ok(id);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("actividades/{id:int}/completar")]
    public async Task<ActionResult> CompletarActividad(int id, CompletarActividadRequest request)
    {
        await _service.CompletarActividadAsync(id, request.Completada);
        return NoContent();
    }

    [HttpDelete("actividades/{id:int}")]
    public async Task<ActionResult> EliminarActividad(int id)
    {
        await _service.EliminarActividadAsync(id);
        return NoContent();
    }

    // ---------- Cotizaciones (agosto 2026) ----------

    [HttpGet("cotizaciones")]
    public async Task<ActionResult<IEnumerable<CotizacionItem>>> ListarCotizaciones([FromQuery] int? clienteId, [FromQuery] string? estado)
        => Ok(await _service.ListarCotizacionesAsync(clienteId, estado));

    [HttpPost("cotizaciones")]
    public async Task<ActionResult> CrearCotizacion(CrearCotizacionRequest request)
    {
        try
        {
            var id = await _service.CrearCotizacionAsync(request, UsuarioActualId);
            return Ok(new { cotizacionId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("cotizaciones/{id:int}/lineas")]
    public async Task<ActionResult<IEnumerable<CotizacionLineaItem>>> ListarLineasCotizacion(int id)
        => Ok(await _service.ListarLineasCotizacionAsync(id));

    [HttpPut("cotizaciones/{id:int}/estado")]
    public async Task<ActionResult> ActualizarEstadoCotizacion(int id, ActualizarEstadoCotizacionRequest request)
    {
        try
        {
            await _service.ActualizarEstadoCotizacionAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("cotizaciones/{id:int}/convertir-a-factura")]
    public async Task<ActionResult<ConvertirCotizacionResponse>> ConvertirCotizacionAFactura(int id)
    {
        try
        {
            var facturaId = await _service.ConvertirCotizacionAFacturaAsync(id, UsuarioActualId);
            return Ok(new ConvertirCotizacionResponse(facturaId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("cotizaciones/{id:int}/enviar-email")]
    public async Task<ActionResult> EnviarEmailCotizacion(int id)
    {
        var enviado = await _service.EnviarEmailCotizacionAsync(id);
        if (!enviado)
            return BadRequest(new { error = "El cliente no tiene email registrado o el servicio de email no está activo." });
        return Ok(new { mensaje = "Email enviado correctamente." });
    }

    [HttpPost("cotizaciones/{id:int}/enviar-whatsapp")]
    public async Task<ActionResult> EnviarWhatsAppCotizacion(int id)
    {
        var (enviado, error) = await _service.EnviarWhatsAppCotizacionAsync(id);
        if (!enviado)
            return BadRequest(new { error = error ?? "No se pudo enviar el mensaje de WhatsApp." });
        return Ok(new { mensaje = "Mensaje de WhatsApp enviado correctamente." });
    }

    // ---------- Segmentación ----------

    [HttpGet("clientes/{id:int}/segmento")]
    public async Task<ActionResult<ClienteSegmentoItem>> ObtenerSegmento(int id)
        => Ok(await _service.ObtenerSegmentoClienteAsync(id));

    // ---------- Línea de crédito ----------

    [HttpGet("clientes/{id:int}/linea-credito")]
    public async Task<ActionResult<LineaCreditoItem?>> ObtenerLineaCredito(int id)
        => Ok(await _service.ObtenerLineaCreditoAsync(id));

    [HttpPut("clientes/{id:int}/linea-credito")]
    public async Task<ActionResult> ActualizarLineaCredito(int id, ActualizarLineaCreditoRequest request)
    {
        await _service.ActualizarLineaCreditoAsync(id, request);
        return NoContent();
    }

    [HttpGet("clientes/{id:int}/linea-credito/disponible")]
    public async Task<ActionResult<DisponibilidadCreditoItem>> DisponibilidadCredito(int id)
        => Ok(await _service.ObtenerDisponibilidadCreditoAsync(id));

    [HttpGet("cotizaciones/{id:int}/pdf")]
    public async Task<IActionResult> DescargarPdf(int id)
    {
        var data = await _service.ObtenerCotizacionParaPdfAsync(id);
        if (data is null) return NotFound();
        var bytes = NexoApi.Common.Export.ExportService.GenerarPdfCotizacion(data);
        return File(bytes, "application/pdf", $"cotizacion_{id}.pdf");
    }

    [HttpDelete("cotizaciones/{id:int}")]
    [Authorize(Roles = "Administracion,Jefes")]
    public async Task<IActionResult> EliminarCotizacion(int id)
    {
        try { await _service.EliminarCotizacionAsync(id); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPost("cotizaciones/expirar-vencidas")]
    [Authorize(Roles = "Administracion,Jefes")]
    public async Task<ActionResult> ExpirarCotizacionesVencidas()
    {
        var afectadas = await _service.ExpireCotizacionesVencidasAsync();
        return Ok(new { mensaje = $"{afectadas} cotización(es) marcada(s) como VENCIDA.", afectadas });
    }

    // ── Exportar clientes ────────────────────────────────────────────────────

    [HttpGet("clientes/exportar/excel")]
    public async Task<IActionResult> ExportarClientesExcel()
    {
        var clientes = await _service.ListarClientesAsync(null, null, null, null);
        var bytes    = ExportService.GenerarExcelClientes(clientes);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"clientes_{NexoApi.Common.ColombiaClock.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("clientes/exportar/pdf")]
    public async Task<IActionResult> ExportarClientesPdf()
    {
        var clientes = await _service.ListarClientesAsync(null, null, null, null);
        var empresa  = await _config.ObtenerEmpresaAsync();
        var bytes    = ExportService.GenerarPdfClientes(clientes, empresa.NombreEmpresa);
        return File(bytes, "application/pdf", $"clientes_{NexoApi.Common.ColombiaClock.Now:yyyyMMdd}.pdf");
    }

    [HttpGet("clientes/{id:int}/prediccion-compra")]
    public async Task<ActionResult<PrediccionCompraItem>> PrediccionCompra(int id)
        => Ok(await _service.PrediccionCompraAsync(id));

    [HttpGet("clientes/panel-recompra")]
    public async Task<ActionResult<IEnumerable<PanelRecompraItem>>> PanelRecompra(
        [FromQuery] int diasUmbral = 7)
        => Ok(await _service.PanelRecompraAsync(diasUmbral));

    [HttpGet("duplicados")]
    public async Task<ActionResult<IEnumerable<DuplicadoCandidatoItem>>> BuscarDuplicados(
        [FromQuery] string? nombre, [FromQuery] string? nit)
        => Ok(await _service.BuscarDuplicadosAsync(nombre, nit));
}
