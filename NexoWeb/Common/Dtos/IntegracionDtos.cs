namespace NexoWeb.Common.Dtos;

public record MapeoArticuloItem(
    int MapeoID, int ArticuloID, string SkuArticulo, string NombreArticulo,
    int CentroCostoID, string CodigoArticuloVisions, bool Estado, DateTime FechaCreacion
);

public record CrearMapeoArticuloRequest(int ArticuloID, int CentroCostoID, string CodigoArticuloVisions);

public record ArticuloPendienteMapeoItem(
    int PendienteID, int CentroCostoID, string CodigoArticuloVisions,
    string? NombreVisions, decimal? CostoVisions, decimal? PrecioVisions,
    decimal CantidadDetectada, DateTime FechaDetectado
);

public record ResolverArticuloPendienteRequest(
    int? ArticuloIDExistente,
    string? SkuNuevo, string? NombreNuevo, decimal? PrecioVentaNuevo, decimal? StockMinimoNuevo
);

public record ResolverTodosResponse(int Total, int Resueltos, int Errores);

public record EstadoIntegracionResponse(
    int AgenteSyncID,
    string Descripcion,
    int CentroCostoID,
    string NombreCentroCosto,
    bool Activo,
    DateTime? UltimoLatido,
    string? VersionAgente,
    string? VersionDisponible,
    int EventosPendientes,
    int EventosProcesadosHoy,
    int VentasImportadasHoy,
    int EventosConError,
    int ArticulosFaltantesVisions = 0,
    bool SyncPausada = false
)
{
    public bool HayActualizacion =>
        VersionAgente is not null && VersionDisponible is not null &&
        VersionAgente != VersionDisponible;
}

public record ConfiguracionAgenteCompletaResponse(
    int AgenteSyncID,
    string? Descripcion,
    int CentroCostoID,
    string NombreCentroCosto,
    bool Activo,
    string? VisionsDbConexion,
    string? NexoApiBaseUrl,
    int IntervalMinutes,
    int IntervalSeconds,
    string? AgentePath,
    string? PrefijosDocumentoVenta,
    DateTime? FechaInicioSyncVentas = null
);

public record ActualizarConfiguracionAgenteRequest(
    string? VisionsDbConexion,
    string? NexoApiBaseUrl,
    int IntervalMinutes,
    int IntervalSeconds,
    string? AgentePath,
    string? PrefijosDocumentoVenta,
    DateTime? FechaInicioSyncVentas = null
);

public record EventoActividadItem(
    long EventoID,
    string TipoEvento,
    string Estado,
    string? ReferenciaVisions,
    string? NombreArticulo,
    decimal Cantidad,
    string? MensajeError,
    DateTime FechaCreacion,
    DateTime? FechaEnvio,
    int IntentosEnvio = 0
);

public record ActividadAgenteResponse(
    int ArticulosEnlazados,
    int FacturasHoy,
    int ComprasHoy,
    int AjustesHoy,
    List<EventoActividadItem> UltimosEventos,
    int TotalArticulos,
    int TotalMarcas,
    int TotalGruposMayor,
    int TotalGruposMenor,
    int TotalClientes,
    int FacturasNexoVisionsHoy,
    int VentasVisionsNexoHoy
);

public record ProgresoSyncResponse(
    int AgenteSyncID,
    string NombreCentroCosto,
    int TotalArticulosNexo,
    int TotalMapeados,
    int TotalPendientesMapeo,
    int TotalEventosProcesados,
    int TotalFacturasVisions,
    DateTime? UltimoLatido,
    int ArticulosMapeadosCompletos = 0,
    int ArticulosMapeadosSinDatos  = 0
);

public record SaludCatalogoResponse(
    int TotalArticulos,
    int ArticulosCompletos,
    int ArticulosSinDatos,
    int TotalMarcas,
    int TotalGruposMayor,
    int TotalGruposMenor,
    int TotalPresentaciones,
    int TotalClientes
);

// ──────────────────────── Ventas Visions (EventosEntrantes agrupados) ────────────────────────
public record VentaVisionsItem(
    int CentroCostoID, string TipDoc, string NroDoc, DateTime Fecha,
    string? NitCliente, string? NombreCliente,
    decimal TotalVenta, int Lineas
);

public record VentasVisionsPaginadasResponse(List<VentaVisionsItem> Items, int Total, int Pagina, int Tamano);

public record VentaVisionsLineaItem(
    string? CodigoArticulo, string? NombreArticulo,
    decimal Cantidad, decimal PrecioUnitario, decimal Subtotal
);
