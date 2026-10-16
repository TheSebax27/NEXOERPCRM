namespace NexoApi.Features.Inventario.Dtos;

public record StockConsolidadoItem(
    int ArticuloID,
    string SKU,
    string Articulo,
    string TipoArticulo,
    string? Unidad,
    decimal? UnidadesPorEmbalaje,
    int BodegaID,
    string Bodega,
    int CentroCostoID,
    string CentroCosto,
    int? LoteID,
    string? NumeroLote,
    DateTime? FechaVencimiento,
    decimal CantidadActual,
    decimal CostoUnitarioLote,
    decimal ValorTotal,
    bool RequierePedido,
    bool TieneImagen,
    decimal StockMinimo,
    decimal CostoPromedio,
    decimal PrecioVenta,
    decimal? ConsumoDiarioProm = null,
    int? DiasAgotamiento = null,
    string? PresentacionNombre = null
);

public record RegistrarBajaRequest(
    int ArticuloID,
    int BodegaID,
    int? LoteID,
    decimal CantidadPerdida,
    int MotivoID,
    string ObservacionDetallada,
    DateTime? Fecha = null
);

public record RegistrarBajaResponse(string CodigoBaja, int BajaID);

public record MotivoPerdidaItem(int MotivoID, string Nombre);

public record OCLineSugeridaItem(
    int ArticuloID,
    string SKU,
    string Articulo,
    string TipoArticulo,
    decimal StockActual,
    decimal StockMinimo,
    decimal ConsumoDiarioProm,
    decimal CantidadSugerida,
    decimal CostoPromedio
);

public record AjustarInventarioRequest(
    int ArticuloID,
    int BodegaID,
    decimal Cantidad,
    decimal CostoUnitario,
    string Motivo,
    DateTime? Fecha = null
);

public record AjustarInventarioResponse(string CodigoAjuste, int AjusteID, decimal NuevoSaldo);

public record KardexMovimientoItem(
    long KardexID,
    DateTime Fecha,
    string SKU,
    string Articulo,
    string? Unidad,
    decimal? UnidadesPorEmbalaje,
    string Bodega,
    string TipoMovimiento,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal CantidadSaldo,
    decimal ValorMovimiento,
    string? ObservacionDetallada,
    string? NumeroLote,
    DateTime? FechaVencimiento
)
{
    public bool EsEntrada => Cantidad >= 0;
}

public record LoteProximoVencerItem(
    string SKU,
    string Articulo,
    string Bodega,
    string? NumeroLote,
    DateTime FechaVencimiento,
    decimal CantidadActual,
    int DiasParaVencer
);

public record ImportarAjustesResult(int Procesados, int Errores, List<string> Mensajes);

public record MermaItem(
    string TipoMerma,
    DateTime Fecha,
    string? OrdenOP,
    int ArticuloID,
    string Articulo,
    string Referencia,
    string? Unidad,
    decimal Cantidad,
    decimal CostoUnitario,
    decimal ValorPerdido,
    string? Motivo,
    string? Observacion
);

public record ResumenMermasResponse(
    decimal TotalValorBajas,
    decimal TotalValorProduccion,
    decimal TotalValor,
    int TotalEventos,
    IEnumerable<MermaItem> Detalle
);