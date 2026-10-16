namespace NexoWeb.Common.Dtos;

public record ComboItem(
    int ComboID, string Nombre, string? Descripcion,
    string Estado, string ModoPrecio,
    DateOnly? FechaInicio, DateOnly? FechaFin,
    decimal? PrecioManual, decimal PorcentajeDescuento,
    decimal? PrecioCalculado,
    string? ImagenBase64, string? ImagenContentType,
    DateTime FechaCreacion,
    List<ComboItemLine> Items,
    string Origen = "NEXO",
    int? CodigoVisions = null,
    string? ReferenciaVisions = null,
    bool ExportadoVisions = false
)
{
    public bool TieneImagen => !string.IsNullOrEmpty(ImagenBase64);
    public string? ImagenDataUri => TieneImagen ? $"data:{ImagenContentType};base64,{ImagenBase64}" : null;
    public decimal PrecioEfectivo
    {
        get
        {
            var base_ = ModoPrecio == "CALCULADO" ? (PrecioCalculado ?? 0) : (PrecioManual ?? 0);
            return PorcentajeDescuento > 0 ? base_ * (1m - PorcentajeDescuento / 100m) : base_;
        }
    }
    public bool Vigente
    {
        get
        {
            var hoy = DateOnly.FromDateTime(NexoWeb.Common.ColombiaClock.Today);
            return (FechaInicio is null || FechaInicio <= hoy) && (FechaFin is null || FechaFin >= hoy);
        }
    }
}

public record ComboDetalle(
    int ComboID, string Nombre, string? Descripcion,
    string? ImagenBase64, string? ImagenContentType,
    string Estado, string ModoPrecio,
    DateOnly? FechaInicio, DateOnly? FechaFin,
    decimal? PrecioManual, decimal PorcentajeDescuento,
    decimal? PrecioCalculado,
    List<ComboItemLine> Items, DateTime FechaCreacion,
    string Origen = "NEXO",
    int? CodigoVisions = null,
    string? ReferenciaVisions = null,
    bool ExportadoVisions = false
);

public record ComboItemLine(
    int ComboItemID, int ArticuloID, string SKU, string NombreArticulo,
    decimal Cantidad, string? Unidad, decimal? PrecioUnitarioSnapshot
);

public record CrearComboRequest(
    string Nombre,
    string? Descripcion,
    string? ImagenBase64,
    string? ImagenContentType,
    string Estado,
    string ModoPrecio,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    decimal? PrecioManual,
    decimal PorcentajeDescuento,
    List<ComboItemInput> Items
);

public record ActualizarComboRequest(
    string Nombre,
    string? Descripcion,
    string? ImagenBase64,
    string? ImagenContentType,
    string Estado,
    string ModoPrecio,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    decimal? PrecioManual,
    decimal PorcentajeDescuento,
    List<ComboItemInput> Items
);

public record ComboItemInput(int ArticuloID, decimal Cantidad);
