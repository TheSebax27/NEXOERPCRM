namespace NexoApi.Features.Produccion.Dtos;

public record EmpleadoRecetaItem(
    int EmpleadoID, string Nombres, string Apellidos, string? Cargo,
    decimal? TarifaHora, decimal? HorasEstimadasPorLote, string? Notas
);

public record EmpleadoOrdenItem(
    int EmpleadoID, string Nombres, string Apellidos, string? Cargo,
    decimal? TarifaHora, decimal? HorasReales, string? Notas
);

public record EmpleadoRecetaInput(int EmpleadoID, decimal? HorasEstimadasPorLote, string? Notas);
public record EmpleadoOrdenInput(int EmpleadoID, decimal? HorasReales, string? Notas);
