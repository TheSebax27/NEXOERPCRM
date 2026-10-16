namespace NexoWeb.Common.Dtos;

public record GastoItem(
    int GastoID, DateTime Fecha, string Categoria, string Descripcion,
    decimal Monto, string? Proveedor, string? Comprobante,
    int? CentroCostoID, string? CentroCosto, string? Notas,
    DateTime FechaCreacion
);

public record CrearGastoRequest(
    DateTime Fecha, string Categoria, string Descripcion, decimal Monto,
    string? Proveedor, string? Comprobante, int? CentroCostoID, string? Notas
);

public record ActualizarGastoRequest(
    DateTime Fecha, string Categoria, string Descripcion, decimal Monto,
    string? Proveedor, string? Comprobante, int? CentroCostoID, string? Notas
);

public record ResumenGastoCategoria(string Categoria, decimal Total, int Cantidad);
public record ResumenGastoMes(int Anio, int Mes, decimal Total);
