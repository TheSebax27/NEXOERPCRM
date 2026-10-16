using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Data;

namespace NexoApi.Features.Produccion;

public record PlanCorteItem(
    int PlanCorteID, int OrdenProduccionID, int? ArticuloLaminaID,
    string NombreLamina, decimal LargoLamina, decimal AnchoLamina, decimal EspesorLamina,
    string UnidadMedida, decimal AnchoSierra, int NumeroLaminas,
    string? Notas, DateTime FechaCreacion);

public record PlanCortePiezaItem(
    int PlanCortePiezaID, int PlanCorteID,
    string NombrePieza, decimal Largo, decimal Ancho, int Cantidad, string? Notas);

public record PlanCorteConPiezas(
    int PlanCorteID, int OrdenProduccionID, int? ArticuloLaminaID,
    string NombreLamina, decimal LargoLamina, decimal AnchoLamina, decimal EspesorLamina,
    string UnidadMedida, decimal AnchoSierra, int NumeroLaminas, string? Notas,
    List<PlanCortePiezaItem> Piezas,
    EstadisticasPlanCorte Estadisticas);

public record EstadisticasPlanCorte(
    decimal AreaLaminaM2, decimal AreaTotalDisponibleM2,
    decimal AreaPiezasM2, decimal AreaKerfM2,
    decimal PorcentajeAprovechamiento, decimal PorcentajeDesperdicioEstimado,
    int TotalPiezas, decimal PiesTablaPiezas);

public record CrearPlanCorteRequest(
    int? ArticuloLaminaID, string NombreLamina,
    decimal LargoLamina, decimal AnchoLamina, decimal EspesorLamina,
    string UnidadMedida, decimal AnchoSierra, int NumeroLaminas, string? Notas);

public record CrearPlanCortePiezaRequest(
    string NombrePieza, decimal Largo, decimal Ancho, int Cantidad, string? Notas);

[ApiController]
[Route("api/produccion")]
[Authorize]
public class PlanCorteController : ControllerBase
{
    private readonly IDbConnectionFactory _db;
    private int UsuarioActualId =>
        int.TryParse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public PlanCorteController(IDbConnectionFactory db) => _db = db;

    [HttpGet("ordenes/{ordenId:int}/plan-corte")]
    public async Task<ActionResult<IEnumerable<PlanCorteItem>>> Listar(int ordenId)
    {
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<PlanCorteItem>(
            @"SELECT PlanCorteID, OrdenProduccionID, ArticuloLaminaID, NombreLamina,
                     LargoLamina, AnchoLamina, EspesorLamina, UnidadMedida,
                     AnchoSierra, NumeroLaminas, Notas, FechaCreacion
              FROM Produccion.PlanCorte
              WHERE OrdenProduccionID = @OrdenID
              ORDER BY PlanCorteID",
            new { OrdenID = ordenId });
        return Ok(items);
    }

    [HttpGet("plan-corte/{planId:int}")]
    public async Task<ActionResult<PlanCorteConPiezas>> Obtener(int planId)
    {
        using var conn = _db.CreateConnection();
        var plan = await conn.QuerySingleOrDefaultAsync<PlanCorteItem>(
            @"SELECT PlanCorteID, OrdenProduccionID, ArticuloLaminaID, NombreLamina,
                     LargoLamina, AnchoLamina, EspesorLamina, UnidadMedida,
                     AnchoSierra, NumeroLaminas, Notas, FechaCreacion
              FROM Produccion.PlanCorte WHERE PlanCorteID = @PlanCorteID",
            new { PlanCorteID = planId });
        if (plan is null) return NotFound();

        var piezas = (await conn.QueryAsync<PlanCortePiezaItem>(
            @"SELECT PlanCortePiezaID, PlanCorteID, NombrePieza, Largo, Ancho, Cantidad, Notas
              FROM Produccion.PlanCortePieza
              WHERE PlanCorteID = @PlanCorteID
              ORDER BY PlanCortePiezaID",
            new { PlanCorteID = planId })).ToList();

        var stats = CalcularEstadisticas(plan, piezas);
        return Ok(new PlanCorteConPiezas(
            plan.PlanCorteID, plan.OrdenProduccionID, plan.ArticuloLaminaID,
            plan.NombreLamina, plan.LargoLamina, plan.AnchoLamina, plan.EspesorLamina,
            plan.UnidadMedida, plan.AnchoSierra, plan.NumeroLaminas, plan.Notas,
            piezas, stats));
    }

    [HttpPost("ordenes/{ordenId:int}/plan-corte")]
    public async Task<ActionResult> Crear(int ordenId, CrearPlanCorteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NombreLamina))
            return BadRequest(new { error = "Nombre de lámina requerido." });
        if (!new[] { "mm", "cm", "m", "in" }.Contains(req.UnidadMedida))
            return BadRequest(new { error = "UnidadMedida inválida. Valores: mm, cm, m, in." });
        if (req.LargoLamina <= 0 || req.AnchoLamina <= 0 || req.EspesorLamina <= 0)
            return BadRequest(new { error = "Dimensiones de lámina deben ser mayores a 0." });
        if (req.NumeroLaminas < 1)
            return BadRequest(new { error = "Número de láminas debe ser al menos 1." });

        using var conn = _db.CreateConnection();
        var existeOrden = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Produccion.OrdenesProduccion WHERE OrdenProduccionID = @ID",
            new { ID = ordenId });
        if (existeOrden == 0) return NotFound(new { error = "Orden no encontrada." });

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Produccion.PlanCorte
                (OrdenProduccionID, ArticuloLaminaID, NombreLamina, LargoLamina, AnchoLamina,
                 EspesorLamina, UnidadMedida, AnchoSierra, NumeroLaminas, Notas, UsuarioID)
              VALUES (@OrdenProduccionID, @ArticuloLaminaID, @NombreLamina, @LargoLamina, @AnchoLamina,
                      @EspesorLamina, @UnidadMedida, @AnchoSierra, @NumeroLaminas, @Notas, @UsuarioID);
              SELECT SCOPE_IDENTITY();",
            new { OrdenProduccionID = ordenId, req.ArticuloLaminaID, req.NombreLamina,
                  req.LargoLamina, req.AnchoLamina, req.EspesorLamina, req.UnidadMedida,
                  req.AnchoSierra, req.NumeroLaminas, req.Notas, UsuarioID = UsuarioActualId });
        return Ok(new { planCorteId = id });
    }

    [HttpPost("plan-corte/{planId:int}/piezas")]
    public async Task<ActionResult> AgregarPieza(int planId, CrearPlanCortePiezaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NombrePieza))
            return BadRequest(new { error = "Nombre de pieza requerido." });
        if (req.Largo <= 0 || req.Ancho <= 0)
            return BadRequest(new { error = "Largo y ancho deben ser mayores a 0." });
        if (req.Cantidad < 1)
            return BadRequest(new { error = "Cantidad debe ser al menos 1." });

        using var conn = _db.CreateConnection();
        var existePlan = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Produccion.PlanCorte WHERE PlanCorteID = @ID",
            new { ID = planId });
        if (existePlan == 0) return NotFound(new { error = "Plan de corte no encontrado." });

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Produccion.PlanCortePieza (PlanCorteID, NombrePieza, Largo, Ancho, Cantidad, Notas)
              VALUES (@PlanCorteID, @NombrePieza, @Largo, @Ancho, @Cantidad, @Notas);
              SELECT SCOPE_IDENTITY();",
            new { PlanCorteID = planId, req.NombrePieza, req.Largo, req.Ancho, req.Cantidad, req.Notas });
        return Ok(new { planCortePiezaId = id });
    }

    [HttpDelete("plan-corte/piezas/{piezaId:int}")]
    public async Task<ActionResult> EliminarPieza(int piezaId)
    {
        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            "DELETE FROM Produccion.PlanCortePieza WHERE PlanCortePiezaID = @ID",
            new { ID = piezaId });
        if (filas == 0) return NotFound();
        return NoContent();
    }

    [HttpDelete("plan-corte/{planId:int}")]
    public async Task<ActionResult> EliminarPlan(int planId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM Produccion.PlanCortePieza WHERE PlanCorteID = @ID",
            new { ID = planId });
        var filas = await conn.ExecuteAsync(
            "DELETE FROM Produccion.PlanCorte WHERE PlanCorteID = @ID",
            new { ID = planId });
        if (filas == 0) return NotFound();
        return NoContent();
    }

    // Calcula estadisticas: area, aprovechamiento, kerf, pies tabla
    private static EstadisticasPlanCorte CalcularEstadisticas(
        PlanCorteItem plan, List<PlanCortePiezaItem> piezas)
    {
        // Factor mm→m
        decimal factor = plan.UnidadMedida == "mm"  ? 1_000_000m
                       : plan.UnidadMedida == "cm"  ? 10_000m
                       : plan.UnidadMedida == "m"   ? 1m
                       : plan.UnidadMedida == "in"  ? 1_550.003m  // in² → m²
                       : 1_000_000m;

        decimal areaLamina   = plan.LargoLamina * plan.AnchoLamina / factor;
        decimal areaTotal    = areaLamina * plan.NumeroLaminas;

        decimal areaPiezas = piezas.Sum(p =>
            (p.Largo * p.Ancho / factor) * p.Cantidad);

        int totalCortes = piezas.Sum(p => p.Cantidad);
        // Kerf: each piece adds 2 cuts (length + width), simplified linear estimate
        decimal kerflargoPiezas = piezas.Sum(p =>
            ((p.Largo + p.Ancho) / (plan.UnidadMedida == "mm" ? 1000m
                                  : plan.UnidadMedida == "cm" ? 100m : 1m))
            * plan.AnchoSierra / 1000m * p.Cantidad);
        decimal areaKerf = kerflargoPiezas * (plan.LargoLamina / (plan.UnidadMedida == "mm" ? 1000m
                                             : plan.UnidadMedida == "cm" ? 100m : 1m));

        decimal aprovechamiento = areaTotal > 0
            ? Math.Round(areaPiezas / areaTotal * 100, 1)
            : 0;
        decimal desperdicio = 100 - aprovechamiento;

        // Pies tabla: 1 pt = 1ft×1ft×1in = 144 in³; para m³: 1 m³ = 423.776 pt
        decimal piesTablaPiezas = piezas.Sum(p => {
            decimal vol = p.Largo * p.Ancho * plan.EspesorLamina;
            decimal pt = plan.UnidadMedida == "mm" ? vol / 1_000_000_000m * 423.776m
                       : plan.UnidadMedida == "cm" ? vol / 1_000_000m * 423.776m
                       : plan.UnidadMedida == "in" ? vol / 144m
                       : vol * 423.776m;  // "m"
            return pt * p.Cantidad;
        });

        return new EstadisticasPlanCorte(
            Math.Round(areaLamina, 4), Math.Round(areaTotal, 4),
            Math.Round(areaPiezas, 4), Math.Round(areaKerf, 4),
            aprovechamiento, Math.Round(desperdicio, 1),
            totalCortes, Math.Round(piesTablaPiezas, 3));
    }
}
