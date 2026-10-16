namespace NexoWeb.Common.Dtos;

// ──────────────────────── Tipos de documento ────────────────────────
public static class TiposDocumento
{
    public static readonly IReadOnlyList<string> Todos =
    [
        "DEVOLUCION PROVEEDOR", "EGRESO", "FACTURA", "GARANTIA",
        "NOTA DEBITO", "NOTA DEVOLUCION", "ORDEN COMPRA",
        "PERDIDA INVENTARIO", "REMISION", "SEPARADOS"
    ];
}

// ──────────────────────── Facturas ────────────────────────
public record FacturaItem(
    int FacturaID, int ClienteID, string Cliente, string? NitCliente,
    DateTime Fecha, string? Notas,
    string TipDoc, string? NroDoc,
    decimal Total, decimal TotalPagado, decimal SaldoPendiente, string Estado,
    bool StockDescontado, bool ProduccionAutoEjecutada, bool VisionsConfirmado,
    int? CentroCostoID, string? CentroCostoNombre
);

public record FacturasPaginadasResponse(List<FacturaItem> Items, int Total, int Pagina, int Tamano);

public record CentroCostoBasicoItem(int CentroCostoID, string Nombre, bool TieneVisions = false);

public record LineaFacturaInput(
    int? ArticuloID, int? ComboID, string? DescripcionLinea,
    decimal Cantidad, decimal PrecioUnitario, string? Nota = null
);

public record CrearFacturaRequest(
    int ClienteID, DateTime Fecha, string? Notas,
    string TipDoc, string? NroDoc,
    List<LineaFacturaInput> Lineas,
    int? CentroCostoID = null
);

public record FacturaLineaItem(
    int LineaID, int FacturaID, int? ArticuloID, string? SkuArticulo, string NombreArticulo,
    int? ComboID, string? DescripcionLinea, string? Nota,
    decimal Cantidad, decimal PrecioUnitario, decimal Subtotal,
    string? Unidad, decimal? UnidadesPorEmbalaje
)
{
    public bool EsUnidadCaja => Unidad == "cja" && UnidadesPorEmbalaje is > 0;
    public bool EsCombo => ComboID.HasValue;
}

public record FacturaLineaStockItem(
    int ArticuloID, string SkuArticulo, string NombreArticulo,
    decimal CantidadFacturada, decimal StockDisponible
)
{
    public bool EsInsuficiente => StockDisponible < CantidadFacturada;
}

// ──────────────────────── Devoluciones ────────────────────────
public record DevolucionItem(
    int DevolucionID,
    string TipoDevolucion,
    int? FacturaOrigenID, string? NroDocOrigen,
    int? ClienteID, string? Cliente,
    int? ProveedorID, string? Proveedor,
    int? CentroCostoID, string? CentroCosto,
    DateTime Fecha, string? Motivo, string? NroDoc,
    decimal Total, bool StockRestituido, bool OrigenVisions,
    string? Usuario, DateTime FechaRegistro
);

public record DevolucionLineaItem(
    int LineaID, int DevolucionID,
    int ArticuloID, string SKU, string Articulo,
    decimal Cantidad, decimal CostoUnitario, decimal Subtotal,
    string? Nota
);

public record DevolucionLineaInput(
    int ArticuloID, decimal Cantidad, decimal CostoUnitario, string? Nota = null
);

public record CrearDevolucionRequest(
    string TipoDevolucion,
    int? FacturaOrigenID,
    int? ClienteID,
    int? ProveedorID,
    int? CentroCostoID,
    DateTime Fecha,
    string? Motivo,
    string? NroDoc,
    List<DevolucionLineaInput> Lineas
);

public record DevolucionesResumenMes(string Mes, int Cantidad, decimal ValorDevuelto);

public record ArticuloMasDevuelto(
    int ArticuloID, string SKU, string Articulo,
    int VecesDevuelto, decimal CantidadTotal, decimal ValorTotal
);

public record DevolucionesAnalytics(
    int TotalDevoluciones,
    decimal ValorTotalDevuelto,
    decimal TasaDevolucionPct,
    List<DevolucionesResumenMes> PorMes,
    List<ArticuloMasDevuelto> TopArticulos
);

// ──────────────────────── Pagos ────────────────────────
public record PagoItem(int PagoID, int FacturaID, decimal Monto, DateTime FechaPago, string MetodoPago, string? Notas, string? Usuario);
public record CrearPagoRequest(int FacturaID, decimal Monto, DateTime FechaPago, string MetodoPago, string? Notas);

// ──────────────────────── Verificacion produccion ────────────────────────
public record InsumoVerificacionItem(
    int ArticuloID, string Nombre, string Unidad,
    decimal CantidadRequerida, decimal StockDisponible
)
{
    public bool EsInsuficiente => StockDisponible < CantidadRequerida;
}

public record VerificarProduccionItem(
    int ArticuloID, string SKU, string Nombre,
    decimal CantidadFacturada, bool TieneReceta, int? RecetaID,
    List<InsumoVerificacionItem> Insumos
)
{
    public bool TieneStockCompleto => TieneReceta && Insumos.All(i => !i.EsInsuficiente);
    public bool TieneInsuficientes => Insumos.Any(i => i.EsInsuficiente);
}

public record AutoProducirResultItem(
    int ArticuloID, string Nombre, bool Exitoso,
    int? OrdenProduccionID, string? Error
);

public record UltimoPrecioItem(decimal Precio, DateTime Fecha, string? NroDoc);
