namespace NexoWeb.Common.Dtos;

// ── Despiece: dimensiones por pieza en BOM ────────────────────────────────

public record DespiecePiezaItem(
    int PiezaID, int RecetaDetalleID, string NombrePieza,
    decimal? Largo, decimal? Ancho, decimal? Espesor,
    string UnidadMedida, decimal Cantidad, string? Notas);

public record CrearDespiecePiezaRequest(
    string NombrePieza, decimal? Largo, decimal? Ancho, decimal? Espesor,
    string UnidadMedida, decimal Cantidad, string? Notas);

public record ActualizarDespiecePiezaRequest(
    string NombrePieza, decimal? Largo, decimal? Ancho, decimal? Espesor,
    string UnidadMedida, decimal Cantidad, string? Notas);

// ── Plan de corte ─────────────────────────────────────────────────────────

public record PlanCorteItem(
    int PlanCorteID, int OrdenProduccionID, int? ArticuloLaminaID,
    string NombreLamina, decimal LargoLamina, decimal AnchoLamina, decimal EspesorLamina,
    string UnidadMedida, decimal AnchoSierra, int NumeroLaminas,
    string? Notas, DateTime FechaCreacion);

public record PlanCortePiezaItem(
    int PlanCortePiezaID, int PlanCorteID,
    string NombrePieza, decimal Largo, decimal Ancho, int Cantidad, string? Notas);

public record EstadisticasPlanCorte(
    decimal AreaLaminaM2, decimal AreaTotalDisponibleM2,
    decimal AreaPiezasM2, decimal AreaKerfM2,
    decimal PorcentajeAprovechamiento, decimal PorcentajeDesperdicioEstimado,
    int TotalPiezas, decimal PiesTablaPiezas);

public record PlanCorteConPiezas(
    int PlanCorteID, int OrdenProduccionID, int? ArticuloLaminaID,
    string NombreLamina, decimal LargoLamina, decimal AnchoLamina, decimal EspesorLamina,
    string UnidadMedida, decimal AnchoSierra, int NumeroLaminas, string? Notas,
    List<PlanCortePiezaItem> Piezas,
    EstadisticasPlanCorte Estadisticas);

public record CrearPlanCorteRequest(
    int? ArticuloLaminaID, string NombreLamina,
    decimal LargoLamina, decimal AnchoLamina, decimal EspesorLamina,
    string UnidadMedida, decimal AnchoSierra, int NumeroLaminas, string? Notas);

public record CrearPlanCortePiezaRequest(
    string NombrePieza, decimal Largo, decimal Ancho, int Cantidad, string? Notas);

// ── Etapas de fabricación por orden ──────────────────────────────────────

public record OrdenEtapaItem(
    int OrdenEtapaID, int OrdenProduccionID, string NombreEtapa, int Orden,
    string Estado, decimal? CantidadRecibida, decimal? CantidadTerminada,
    decimal? CantidadRechazada, int? EmpleadoID, string? NombreEmpleado,
    DateTime? FechaInicio, DateTime? FechaFin, int? TiempoMinutos, string? Notas);

public record InicializarEtapasRequest(List<string>? Etapas);

public record ActualizarEtapaRequest(
    string Estado, decimal? CantidadRecibida, decimal? CantidadTerminada,
    decimal? CantidadRechazada, int? EmpleadoID,
    DateTime? FechaInicio, DateTime? FechaFin, int? TiempoMinutos, string? Notas);

// ── Sobrantes de material ─────────────────────────────────────────────────

public record SobranteMaterialItem(
    int SobranteID, int? OrdenProduccionID, string? CodigoOP,
    int ArticuloID, string NombreArticulo,
    decimal Largo, decimal Ancho, decimal Espesor,
    string UnidadMedida, int CantidadPiezas, string Estado,
    int? BodegaID, string? NombreBodega, string? Notas, DateTime FechaRegistro);

public record CrearSobranteRequest(
    int? OrdenProduccionID, int ArticuloID,
    decimal Largo, decimal Ancho, decimal Espesor,
    string UnidadMedida, int CantidadPiezas,
    int? BodegaID, string? Notas);

public record ActualizarEstadoSobranteRequest(string Estado);
