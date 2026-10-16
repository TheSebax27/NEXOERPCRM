using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Security;
using NexoApi.Features.Compras;
using NexoApi.Features.Compras.Dtos;
using NexoApi.Features.Integracion.Dtos;
using NexoApi.Features.Marketing;

namespace NexoApi.Features.Integracion;

[ApiController]
[Route("api/integracion")]
public class IntegracionController : ControllerBase
{
    private readonly IIntegracionService _service;
    private readonly IOrdenesCompraService _compras;
    private readonly IComboService _combos;
    private readonly ILogger<IntegracionController> _logger;

    private readonly VentaVisionsPdfService _ventaPdf;

    public IntegracionController(IIntegracionService service, IOrdenesCompraService compras, IComboService combos, ILogger<IntegracionController> logger, VentaVisionsPdfService ventaPdf)
    {
        _service  = service;
        _compras  = compras;
        _combos   = combos;
        _logger   = logger;
        _ventaPdf = ventaPdf;
    }

    private int CentroCostoDelAgente =>
        int.Parse(User.FindFirstValue("CentroCostoId")!);

    /// <summary>Llamado por el Agente de Sincronizacion. Requiere header X-Api-Key.</summary>
    [HttpGet("eventos-pendientes")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<IEnumerable<EventoPendienteItem>>> ObtenerEventosPendientes()
    {
        return Ok(await _service.ObtenerEventosPendientesAsync(CentroCostoDelAgente));
    }

    /// <summary>Llamado por el Agente tras aplicar exitosamente un evento en Visions.</summary>
    [HttpPost("eventos-salientes/{id:long}/confirmar")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ConfirmarEventoSaliente(long id)
    {
        await _service.ConfirmarEventoSalienteAsync(id, CentroCostoDelAgente);
        return Ok(new { mensaje = "Evento confirmado." });
    }

    /// <summary>Llamado por el Agente cuando detecta una venta nueva en Visions.</summary>
    [HttpPost("eventos-entrantes")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> RegistrarEventoEntrante(RegistrarEventoEntranteRequest request)
    {
        try
        {
            await _service.RegistrarEventoEntranteAsync(request, CentroCostoDelAgente);
            return Ok(new { mensaje = "Evento entrante procesado." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando EventoEntrante {IdEventoExterno} TipoEvento={TipoEvento} Referencia={Referencia}",
                request.IdEventoExterno, request.TipoEvento, request.CodigoArticuloVisions);
            throw;
        }
    }

    /// <summary>Genera la API Key de un cliente nuevo. Usa JWT normal, solo Administracion -- nada que ver con el agente.</summary>
    [HttpPost("agentes/api-key")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<GenerarApiKeyResponse>> GenerarApiKey(GenerarApiKeyRequest request)
    {
        return Ok(await _service.GenerarApiKeyAsync(request));
    }

    /// <summary>Llamado por el Agente en cada ronda: trae la configuracion que el Administracion
    /// dejo en NEXO (activo, prefijos de documento de venta) para aplicarla en Visions. Asi
    /// esos valores solo se editan desde la web, nunca directo por SQL contra la base de Visions.</summary>
    [HttpGet("configuracion")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<ConfiguracionAgenteResponse>> ObtenerConfiguracion()
    {
        return Ok(await _service.ObtenerConfiguracionAgenteAsync(CentroCostoDelAgente));
    }

    // ---------- Mapeo de Articulos (uso del Administracion desde NEXO Web, JWT normal) ----------

    [HttpGet("mapeos")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<MapeoArticuloItem>>> ListarMapeos([FromQuery] int centroCostoId)
    {
        return Ok(await _service.ListarMapeosAsync(centroCostoId));
    }

    [HttpPost("mapeos")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> CrearMapeo(CrearMapeoArticuloRequest request)
    {
        await _service.CrearMapeoAsync(request);
        return Ok(new { mensaje = "Articulo mapeado y encolado para crear/actualizar en Visions." });
    }

    [HttpGet("articulos-pendientes")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ArticuloPendienteMapeoItem>>> ListarArticulosPendientes([FromQuery] int centroCostoId)
    {
        return Ok(await _service.ListarArticulosPendientesMapeoAsync(centroCostoId));
    }

    [HttpPost("articulos-pendientes/{id:int}/resolver")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ResolverArticuloPendiente(int id, ResolverArticuloPendienteRequest request)
    {
        try
        {
            await _service.ResolverArticuloPendienteAsync(id, request);
            return Ok(new { mensaje = "Articulo vinculado correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("articulos-pendientes/resolver-todos")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<ResolverTodosResponse>> ResolverTodos([FromQuery] int centroCostoId)
    {
        var resultado = await _service.ResolverTodosAsync(centroCostoId);
        return Ok(resultado);
    }

    // ---------- Latido y monitoreo ----------

    /// <summary>Llamado por el Agente en cada ronda para registrar que sigue activo
    /// y actualizar UltimaConexion. Devuelve hora del servidor y eventos pendientes.</summary>
    [HttpPost("latido")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<LatidoResponse>> RegistrarLatido([FromBody] LatidoRequest? request = null)
    {
        try
        {
            var respuesta = await _service.RegistrarLatidoAsync(CentroCostoDelAgente, request?.Version);
            return Ok(respuesta);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Devuelve el estado de todos los agentes registrados.
    /// Usado por la pantalla de monitoreo de NexoWeb (solo Administracion).</summary>
    [HttpGet("estado")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<IEnumerable<EstadoIntegracionResponse>>> ObtenerEstado()
    {
        return Ok(await _service.ObtenerEstadoIntegracionAsync());
    }

    /// <summary>Progreso de sincronización: articulos mapeados, ventas procesadas, facturas creadas.</summary>
    [HttpGet("progreso-sync")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<IEnumerable<ProgresoSyncResponse>>> ObtenerProgreso()
    {
        return Ok(await _service.ObtenerProgresoSyncAsync());
    }

    /// <summary>Llamado por el Agente cuando no pudo aplicar un evento en Visions.
    /// Tras 3 intentos fallidos el evento pasa a ERROR y deja de reintentarse.</summary>
    [HttpPost("eventos-salientes/{id:long}/fallar")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> RegistrarFallo(long id, RegistrarFalloRequest request)
    {
        await _service.RegistrarFalloEventoAsync(id, CentroCostoDelAgente, request.MensajeError);
        return Ok(new { mensaje = "Fallo registrado." });
    }

    // ---------- Gestión de configuración del agente (Administracion) ----------

    [HttpGet("agentes/{id:int}/config")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<ConfiguracionAgenteCompletaResponse>> ObtenerConfigCompleta(int id)
    {
        try { return Ok(await _service.ObtenerConfiguracionCompletaAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPut("agentes/{id:int}/config")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarConfigCompleta(int id, ActualizarConfiguracionAgenteRequest request)
    {
        try
        {
            await _service.ActualizarConfiguracionCompletaAsync(id, request);
            return Ok(new { mensaje = "Configuracion actualizada." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Genera el ZIP del instalador (exe + appsettings con key fresca) y devuelve un token de descarga.</summary>
    [HttpPost("agentes/{id:int}/preparar-descarga")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> PrepararDescarga(int id)
    {
        try
        {
            var token = await _service.PrepararDescargaInstaladorAsync(id);
            return Ok(new { token });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex)      { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Descarga el instalador .exe usando el token de un solo uso generado por PrepararDescarga.</summary>
    [HttpGet("descargar-agente/{token}")]
    [AllowAnonymous]
    public ActionResult DescargarAgente(string token)
    {
        var bytes = _service.ObtenerPaqueteInstalador(token);
        if (bytes is null)
            return NotFound(new { error = "El enlace de descarga expiró o ya fue usado. Genera uno nuevo." });

        return File(bytes, "application/octet-stream", "NexoAgente-Setup.exe");
    }

    // ──────── Sincronizacion de catalogos (bidireccional) ────────

    [HttpGet("catalogo/marcas")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ListarMarcasSync() =>
        Ok(await _service.ListarMarcasSyncAsync());

    [HttpPost("catalogo/marcas/upsert")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> UpsertMarca(MarcaSyncItem item)
    {
        await _service.UpsertMarcaAsync(item);
        return Ok(new { mensaje = "Marca sincronizada." });
    }

    [HttpGet("catalogo/grupos-mayor")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ListarGruposMayorSync() =>
        Ok(await _service.ListarGruposMayorSyncAsync());

    [HttpPost("catalogo/grupos-mayor/upsert")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> UpsertGrupoMayor(GrupoMayorSyncItem item)
    {
        await _service.UpsertGrupoMayorAsync(item);
        return Ok(new { mensaje = "Grupo mayor sincronizado." });
    }

    [HttpGet("catalogo/grupos-menor")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ListarGruposMenorSync() =>
        Ok(await _service.ListarGruposMenorSyncAsync());

    [HttpPost("catalogo/grupos-menor/upsert")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> UpsertGrupoMenor(GrupoMenorSyncItem item)
    {
        await _service.UpsertGrupoMenorAsync(item);
        return Ok(new { mensaje = "Grupo menor sincronizado." });
    }

    [HttpGet("catalogo/iva")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ListarIvaSync() =>
        Ok(await _service.ListarIvaSyncAsync());

    [HttpPost("catalogo/iva/upsert")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> UpsertIva(IvaSyncItem item)
    {
        await _service.UpsertIvaAsync(item);
        return Ok(new { mensaje = "IVA sincronizado." });
    }

    [HttpGet("catalogo/presentaciones")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ListarPresentacionesSync() =>
        Ok(await _service.ListarPresentacionesSyncAsync());

    [HttpPost("catalogo/presentaciones/upsert")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> UpsertPresentacion(PresentacionSyncItem item)
    {
        await _service.UpsertPresentacionAsync(item);
        return Ok(new { mensaje = "Presentacion sincronizada." });
    }

    // ──────── Facturas NEXO → Visions ────────

    [HttpGet("facturas-para-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ListarFacturasParaVisions() =>
        Ok(await _service.ListarFacturasParaVisionsAsync(CentroCostoDelAgente));

    [HttpPost("facturas/{id:int}/marcar-exportada-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> MarcarFacturaExportadaVisions(int id)
    {
        await _service.MarcarFacturaExportadaVisionsAsync(id, CentroCostoDelAgente);
        return Ok(new { mensaje = "Factura marcada como exportada a Visions." });
    }

    [HttpPut("facturas/{id:int}/numero-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ActualizarNumeroVisions(int id, ActualizarNumeroVisionsRequest request)
    {
        await _service.ActualizarNumeroVisionsAsync(id, request);
        return Ok(new { mensaje = "Número de factura actualizado desde Visions." });
    }

    /// <summary>Llamado por el Agente al iniciar. NEXO genera EventosSalientes SINCRONIZAR_ARTICULO
    /// para todos los articulos que este CC deberia tener pero aun no tiene evento pendiente.
    /// Idempotente: NOT EXISTS previene duplicados.</summary>
    [HttpPost("sync/solicitar-catalogo-completo")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> SolicitarCatalogoCompleto()
    {
        await _service.SolicitarSyncCatalogoCompletoAsync(CentroCostoDelAgente);
        return Ok(new { mensaje = "Solicitud de catalogo completo procesada." });
    }

    /// <summary>Llamado desde la UI web (admin). Solicita sync de catalogo completo para el CC indicado.</summary>
    [HttpPost("sync/solicitar-catalogo-completo-admin")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> SolicitarCatalogoCompletoAdmin([FromQuery] int centroCostoId)
    {
        await _service.SolicitarSyncCatalogoCompletoAsync(centroCostoId);
        return Ok(new { mensaje = "Solicitud de catalogo completo procesada." });
    }

    /// <summary>Llamado por el Agente cuando detecta un cambio de precio/nombre en TARJETA de Visions.
    /// Actualiza Catalogo.Tarjetas en NEXO si el cambio de Visions es mas reciente.</summary>
    [HttpPost("sync/articulo-desde-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> SyncArticuloDesdeVisions(SyncArticuloDesdeVisionsRequest request)
    {
        await _service.SyncArticuloDesdeVisionsAsync(request);
        return Ok(new { mensaje = "Sync procesado." });
    }

    /// <summary>Llamado por el Agente cuando detecta que una referencia fue eliminada de dbo.TARJETA en Visions.</summary>
    [HttpPost("sync/articulo-inactivar")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> InactivarArticuloDesdeVisions([FromQuery] string referencia, [FromQuery] string centroCostoVisions = "")
    {
        await _service.InactivarArticuloDesdeVisionsAsync(referencia, centroCostoVisions);
        return Ok(new { mensaje = "Articulo inactivado." });
    }

    /// <summary>Llamado por el Agente para obtener articulos con imagen actualizada en NEXO y llevarlos a Visions.</summary>
    [HttpGet("sync/articulos-imagen-actualizados")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ListarArticulosImagenActualizados([FromQuery] DateTime? desde)
        => Ok(await _service.ListarArticulosConImagenActualizadaAsync(desde));

    /// <summary>Llamado por el Agente para obtener clientes activos de NEXO y sincronizarlos a NEXO_Clientes en Visions.</summary>
    [HttpGet("sync/clientes")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<IEnumerable<ClienteParaSyncDto>>> ListarClientesParaSync([FromQuery] DateTime? desde)
    {
        return Ok(await _service.ListarClientesParaSyncAsync(desde, CentroCostoDelAgente));
    }

    /// <summary>Llamado por el Agente cuando detecta un cliente/usuario nuevo o modificado en dbo.USUARIOS de Visions.</summary>
    [HttpPost("sync/cliente-desde-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> SyncClienteDesdeVisions(SyncClienteDesdeVisionsRequest request)
    {
        await _service.SyncClienteDesdeVisionsAsync(request);
        return Ok(new { mensaje = "Cliente sincronizado." });
    }

    /// <summary>Llamado por el Agente para obtener proveedores activos de NEXO y sincronizarlos a dbo.USUARIOS en Visions.</summary>
    [HttpGet("sync/proveedores")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<IEnumerable<ProveedorParaSyncDto>>> ListarProveedoresParaSync()
    {
        return Ok(await _service.ListarProveedoresParaSyncAsync(CentroCostoDelAgente));
    }

    /// <summary>Llamado por el Agente cuando detecta un proveedor nuevo o modificado en dbo.USUARIOS de Visions (PROVEEDOR=1).</summary>
    [HttpPost("sync/proveedor-desde-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> SyncProveedorDesdeVisions(SyncProveedorDesdeVisionsRequest request)
    {
        await _service.SyncProveedorDesdeVisionsAsync(request);
        return Ok(new { mensaje = "Proveedor sincronizado." });
    }

    [HttpGet("agentes/{id:int}/actividad")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<ActividadAgenteResponse>> ObtenerActividad(int id)
    {
        try { return Ok(await _service.ObtenerActividadAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Desactiva (soft-delete) un agente. El servicio Windows puede seguir corriendo hasta que se detenga manualmente.</summary>
    [HttpDelete("agentes/{id:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> DesactivarAgente(int id)
    {
        try
        {
            await _service.DesactivarAgenteAsync(id);
            return Ok(new { mensaje = "Agente desactivado." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Resetea a PENDIENTE todos los EventosSalientes en estado ERROR del agente.</summary>
    [HttpPost("agentes/{id:int}/reintentar")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ReintentarPendientes(int id)
    {
        try
        {
            var count = await _service.ReintentarPendientesAsync(id);
            return Ok(new { mensaje = $"{count} evento(s) reseteados a PENDIENTE.", count });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Reprocesa EventosEntrantes con Procesado=0 del agente (Visions→NEXO pendientes).</summary>
    [HttpPost("agentes/{id:int}/reprocesar-entrantes")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ReprocesarEntrantes(int id)
    {
        try
        {
            var count = await _service.ReprocesarEntrantesAsync(id);
            return Ok(new { mensaje = $"{count} evento(s) entrante(s) reprocesados.", count });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Totales en tiempo real del catálogo sincronizado: artículos completos/sin datos, clientes, etc.</summary>
    [HttpGet("salud-catalogo")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<SaludCatalogoResponse>> ObtenerSaludCatalogo()
        => Ok(await _service.ObtenerSaludCatalogoAsync());

    /// <summary>Ventas registradas desde Visions en EventosEntrantes, agrupadas por documento.</summary>
    [Authorize(Roles = "Administracion")]
    [HttpGet("ventas-visions")]
    public async Task<ActionResult<VentasVisionsPaginadasResponse>> ListarVentasVisions(
        [FromQuery] int? centroCostoId,
        [FromQuery] string? tipDoc,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 50)
        => Ok(await _service.ListarVentasVisionsAsync(centroCostoId, tipDoc, desde, hasta, pagina, tamano));

    /// <summary>PDF de un documento de Visions.</summary>
    [Authorize(Roles = "Administracion")]
    [HttpGet("ventas-visions/pdf")]
    public async Task<ActionResult> DescargarPdfVentaVisions(
        [FromQuery] int centroCostoId,
        [FromQuery] string tipDoc,
        [FromQuery] string nroDoc)
    {
        try
        {
            var bytes = await _ventaPdf.GenerarAsync(centroCostoId, tipDoc, nroDoc);
            return File(bytes, "application/pdf", $"visions-{nroDoc}.pdf");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Líneas de un documento de Visions (EventosEntrantes).</summary>
    [Authorize(Roles = "Administracion")]
    [HttpGet("ventas-visions/lineas")]
    public async Task<ActionResult<IEnumerable<VentaVisionsLineaItem>>> ListarLineasVentaVisions(
        [FromQuery] int centroCostoId,
        [FromQuery] string tipDoc,
        [FromQuery] string nroDoc)
        => Ok(await _service.ListarLineasVentaVisionsAsync(centroCostoId, tipDoc, nroDoc));

    /// <summary>Genera solo el appsettings.json (para actualizar config en agentes ya instalados).</summary>
    [HttpGet("agentes/{id:int}/appsettings-json")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<string>> GenerarAppsettingsJson(int id)
    {
        try
        {
            var contenido = await _service.PrepararAppsettingsConKeyFrescaAsync(id);
            return Content(contenido, "application/json");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ──────── Pedidos NEXO → Visions (Entradas) ────────

    [HttpGet("pedidos-para-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<IEnumerable<PedidoParaVisionsDto>>> ListarPedidosParaVisions() =>
        Ok(await _compras.ListarPedidosParaVisionsAsync(CentroCostoDelAgente));

    [HttpPost("pedidos/{id:int}/marcar-exportado-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> MarcarPedidoExportadoVisions(int id)
    {
        await _compras.MarcarPedidoExportadoVisionsAsync(id);
        return Ok(new { mensaje = "Pedido marcado como exportado a Visions." });
    }

    [HttpPut("pedidos/{id:int}/numero-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ActualizarNumeroPedidoVisions(int id, ActualizarNumeroPedidoVisionsRequest request)
    {
        await _compras.ActualizarNumeroPedidoVisionsAsync(id, request);
        return Ok(new { mensaje = "Número de pedido actualizado desde Visions." });
    }

    [HttpPost("pedidos/{id:int}/auto-recibir-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> AutoRecibirPedidoVisions(int id, [FromQuery] string nroDoc)
    {
        await _compras.AutoRecibirDesdeVisionsAsync(id, nroDoc);
        return Ok(new { mensaje = "Pedido auto-recibido desde Visions." });
    }

    // ──────────── Limpieza staging Visions (facturas/pedidos eliminados en NEXO) ────────────

    [HttpGet("pendientes-limpieza-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult<IEnumerable<PendienteLimpiezaVisions>>> ListarPendientesLimpiezaVisions()
        => Ok(await _service.ListarPendientesLimpiezaVisionsAsync());

    [HttpPost("pendientes-limpieza-visions/{id:int}/completar")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> CompletarLimpiezaVisions(int id)
    {
        await _service.MarcarLimpiezaVisionsCompletadaAsync(id);
        return Ok(new { mensaje = "Limpieza completada." });
    }

    [HttpGet("agentes/{id:int}/sincantsa")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<SincAntsaResponse>> ObtenerSincAntsa(int id)
        => Ok(new SincAntsaResponse(await _service.ObtenerSincAntsaAsync(id)));

    [HttpPut("agentes/{id:int}/sincantsa")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> SetSincAntsa(int id, SetSincAntsaRequest request)
    {
        await _service.SetSincAntsaAsync(id, request.Activo);
        return NoContent();
    }

    /// <summary>Pausa o reanuda la sincronización de Visions para un agente sin detener el servicio.</summary>
    [HttpPut("agentes/{id:int}/sync-pausada")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> SetSyncPausada(int id, [FromBody] bool pausada)
    {
        try
        {
            await _service.SetSyncPausadaAsync(id, pausada);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Borra EventosEntrantes y resetea stock a 0 para ese CC. Solo para limpiar datos de prueba pre-go-live.</summary>
    [HttpPost("agentes/{id:int}/purgar-ventas-test")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> PurgarVentasTest(int id)
    {
        try
        {
            var borrados = await _service.PurgarVentasTestAsync(id);
            return Ok(new { mensaje = $"Purga completada. {borrados} evento(s) borrado(s). El agente limpiará Visions en el próximo ciclo.", borrados });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Llamado por el agente tras limpiar NEXO_VentasExportadas/NEXO_EntradasExportadas en Visions.</summary>
    [HttpPost("reset-purga-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ResetPurgaVisions()
    {
        await _service.ResetPurgaVisionsAsync(CentroCostoDelAgente);
        return NoContent();
    }

    /// <summary>Admin solicita reproceso de datos maestros (artículos, clientes, proveedores).</summary>
    [HttpPost("agentes/{id:int}/reprocesar")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> SolicitarReproceso(int id, [FromBody] SolicitarReprocesoRequest request)
    {
        var tiposValidos = new[] { "Articulos", "Clientes", "Proveedores", "Todo" };
        if (!tiposValidos.Contains(request.Tipo))
            return BadRequest(new { error = $"Tipo debe ser uno de: {string.Join(", ", tiposValidos)}." });
        try
        {
            await _service.SolicitarReprocesoAsync(id, request.Tipo);
            return Ok(new { mensaje = $"Reproceso de {request.Tipo} solicitado. El agente lo ejecutará en el próximo ciclo." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>UI consulta el estado del reproceso en curso (polling).</summary>
    [HttpGet("agentes/{id:int}/estado-reproceso")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<EstadoReprocesoDto?>> ObtenerEstadoReproceso(int id)
    {
        var estado = await _service.ObtenerEstadoReprocesoAsync(id);
        return Ok(estado);
    }

    /// <summary>Agente reporta progreso durante el reproceso.</summary>
    [HttpPost("reproceso/actualizar-progreso")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ActualizarProgresoReproceso([FromBody] ActualizarProgresoReprocesoRequest request)
    {
        await _service.ActualizarProgresoReprocesoAsync(CentroCostoDelAgente, request.Procesados, request.TotalEstimado, request.Estado);
        return NoContent();
    }

    /// <summary>Agente limpia las banderas de reproceso al terminar.</summary>
    [HttpPost("reset-banderas-reproceso")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ResetBanderasReproceso()
    {
        await _service.ResetBanderasReprocesoAsync(CentroCostoDelAgente);
        return NoContent();
    }

    // ──────────────── Sync bidireccional Combos ────────────────

    /// <summary>Agente obtiene combos NEXO activos para crear/actualizar COMBO en Visions.</summary>
    [HttpGet("sync/combos")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> ListarCombosParaVisions([FromQuery] DateTime? desde)
        => Ok(await _combos.ListarParaVisionsAsync(desde));

    /// <summary>Agente confirma que creó el COMBO en Visions con el CODIGO y REFERENCIA asignados.</summary>
    [HttpPut("sync/combos/{id:int}/exportado")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> MarcarComboExportado(int id, [FromBody] MarcarComboExportadoRequest r)
    {
        await _combos.MarcarExportadoVisionsAsync(id, r.CodigoVisions, r.ReferenciaVisions);
        return Ok(new { mensaje = "Combo marcado como exportado a Visions." });
    }

    /// <summary>Agente sincroniza un combo creado/modificado en Visions hacia NEXO.</summary>
    [HttpPost("sync/combo-desde-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> SyncComboDesdeVisions([FromBody] SyncComboDesdeVisionsRequest request)
    {
        var comboId = await _combos.SyncDesdeVisionsAsync(request);
        return Ok(new { ComboID = comboId, mensaje = "Combo sincronizado desde Visions." });
    }

    /// <summary>Agente informa que un COMBO fue eliminado en Visions; NEXO lo marca Inactivo.</summary>
    [HttpPost("sync/combo-eliminar-visions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<ActionResult> EliminarComboVisions([FromQuery] int codigoVisions)
    {
        await _combos.EliminarComboVisionsAsync(codigoVisions);
        return Ok(new { mensaje = "Combo inactivado." });
    }
}