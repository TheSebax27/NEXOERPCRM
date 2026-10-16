using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Data;
using NexoApi.Features.Rrhh.Dtos;

namespace NexoApi.Features.Rrhh;

[ApiController]
[Route("api/rrhh/empleados")]
[Authorize]
public class EmpleadosController : ControllerBase
{
    private readonly IRrhhService _service;

    public EmpleadosController(IRrhhService service)
    {
        _service = service;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmpleadoItem>>> Listar([FromQuery] bool soloActivos = false)
        => Ok(await _service.ListarEmpleadosAsync(soloActivos));

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> Crear(CrearEmpleadoRequest request)
    {
        var id = await _service.CrearEmpleadoAsync(request);
        return CreatedAtAction(nameof(Listar), new { EmpleadoID = id }, new { empleadoId = id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<ActionResult> Actualizar(int id, ActualizarEmpleadoRequest request)
    {
        try
        {
            await _service.ActualizarEmpleadoAsync(id, request, UsuarioActualId);
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

    // ================= Foto (opcional, una sola por empleado) =================

    [HttpGet("{id:int}/foto")]
    [AllowAnonymous]
    public async Task<IActionResult> ObtenerFoto(int id)
    {
        var foto = await _service.ObtenerFotoEmpleadoAsync(id);
        if (foto is null)
            return NotFound();

        return File(foto.Value.Datos, foto.Value.ContentType);
    }

    [HttpPut("{id:int}/foto")]
    [Authorize]
    public async Task<ActionResult> ActualizarFoto(int id, ActualizarFotoEmpleadoRequest request)
    {
        byte[] fotoBytes;
        try { fotoBytes = Convert.FromBase64String(request.Base64); }
        catch { return BadRequest(new { error = "El archivo no es Base64 válido." }); }
        if (fotoBytes.Length > 1_000_000)
            return BadRequest(new { error = "La foto es muy grande (máximo 1 MB)." });

        try
        {
            await _service.ActualizarFotoEmpleadoAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ================= Carnet PDF =================

    [HttpGet("{id:int}/carnet")]
    public async Task<IActionResult> Carnet(int id, [FromServices] CarnetEmpleadoService carnet)
    {
        try
        {
            var pdf = await carnet.GenerarAsync(id);
            return File(pdf, "application/pdf", $"carnet-empleado-{id}.pdf");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("{id:int}/foto")]
    [Authorize]
    public async Task<ActionResult> EliminarFoto(int id)
    {
        try
        {
            await _service.EliminarFotoEmpleadoAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ================= Historial laboral (agosto 2026, RRHH v2) =================
    // Se genera automatico -- no hay POST, solo lectura.

    [HttpGet("{id:int}/historial")]
    public async Task<ActionResult<IEnumerable<HistorialLaboralItem>>> ListarHistorial(int id)
        => Ok(await _service.ListarHistorialLaboralAsync(id));

    // ================= Documentos (agosto 2026, RRHH v2) =================

    [HttpGet("{id:int}/documentos")]
    public async Task<ActionResult<IEnumerable<EmpleadoDocumentoItem>>> ListarDocumentos(int id)
        => Ok(await _service.ListarDocumentosAsync(id));

    [HttpPost("{id:int}/documentos")]
    [Authorize]
    public async Task<ActionResult> SubirDocumento(int id, SubirDocumentoEmpleadoRequest request)
    {
        byte[] docBytes;
        try { docBytes = Convert.FromBase64String(request.Base64); }
        catch { return BadRequest(new { error = "El archivo no es Base64 válido." }); }
        if (docBytes.Length > 5_000_000)
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
    [Authorize]
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

    // ================= Evaluaciones de desempeño (agosto 2026, RRHH v2) =================

    [HttpGet("{id:int}/evaluaciones")]
    public async Task<ActionResult<IEnumerable<EvaluacionItem>>> ListarEvaluaciones(int id)
        => Ok(await _service.ListarEvaluacionesAsync(id));

    [HttpPost("evaluaciones")]
    [Authorize]
    public async Task<ActionResult> CrearEvaluacion(CrearEvaluacionRequest request)
    {
        try
        {
            var id = await _service.CrearEvaluacionAsync(request, UsuarioActualId);
            return Ok(new { evaluacionId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ================= Capacitaciones (agosto 2026, RRHH v2) =================

    [HttpGet("{id:int}/capacitaciones")]
    public async Task<ActionResult<IEnumerable<CapacitacionItem>>> ListarCapacitaciones(int id)
        => Ok(await _service.ListarCapacitacionesAsync(id));

    [HttpPost("capacitaciones")]
    [Authorize]
    public async Task<ActionResult> CrearCapacitacion(CrearCapacitacionRequest request)
    {
        var id = await _service.CrearCapacitacionAsync(request, UsuarioActualId);
        return Ok(new { capacitacionId = id });
    }

    // ================= Organigrama (agosto 2026, RRHH v2) =================

    [HttpGet("organigrama")]
    public async Task<ActionResult<IEnumerable<OrganigramaNodo>>> ObtenerOrganigrama()
        => Ok(await _service.ObtenerOrganigramaAsync());

    // ================= Saldo de vacaciones (oct 2026) =================
    // 15 días por año trabajado (pro-rata), menos días tomados en ausencias VACACIONES+APROBADA.

    [HttpGet("{id:int}/saldo-vacaciones")]
    public async Task<ActionResult<SaldoVacacionesItem>> SaldoVacaciones(
        int id, [FromServices] IDbConnectionFactory db)
    {
        using var conn = db.CreateConnection();
        var saldo = await conn.QuerySingleOrDefaultAsync<SaldoVacacionesItem>(
            @"SELECT
                FLOOR(DATEDIFF(day, e.FechaIngreso, GETDATE()) / 365.0 * 15) AS DiasGanados,
                ISNULL((
                    SELECT SUM(DATEDIFF(day, FechaInicio, FechaFin) + 1)
                    FROM Rrhh.Ausencias
                    WHERE EmpleadoID = e.EmpleadoID
                      AND Tipo = 'VACACIONES' AND Estado = 'APROBADA'
                ), CAST(0 AS INT)) AS DiasTomados,
                FLOOR(DATEDIFF(day, e.FechaIngreso, GETDATE()) / 365.0 * 15)
                - ISNULL((
                    SELECT SUM(DATEDIFF(day, FechaInicio, FechaFin) + 1)
                    FROM Rrhh.Ausencias
                    WHERE EmpleadoID = e.EmpleadoID
                      AND Tipo = 'VACACIONES' AND Estado = 'APROBADA'
                ), CAST(0 AS INT)) AS DiasDisponibles
              FROM Rrhh.Empleados e
              WHERE e.EmpleadoID = @EmpleadoID
                AND e.FechaIngreso IS NOT NULL",
            new { EmpleadoID = id });

        return Ok(saldo ?? new SaldoVacacionesItem(0, 0, 0));
    }

    // ================= Alertas RRHH (oct 2026) =================
    // Capacitaciones por vencer (30 días), contratos por vencer (30 días) y ausencias pendientes.

    [HttpGet("alertas")]
    public async Task<ActionResult<IEnumerable<AlertaRrhh>>> Alertas(
        [FromServices] IDbConnectionFactory db)
    {
        using var conn = db.CreateConnection();
        var alertas = new List<AlertaRrhh>();

        var capacitaciones = await conn.QueryAsync<AlertaRrhh>(
            @"SELECT
                'CAPACITACION_VENCE' AS Tipo,
                c.Nombre + ' vence el ' + CONVERT(NVARCHAR(10), c.FechaVencimiento, 103) AS Descripcion,
                e.Nombres + ' ' + e.Apellidos AS Empleado,
                e.EmpleadoID,
                CAST(c.FechaVencimiento AS DATETIME2) AS FechaReferencia
              FROM Rrhh.Capacitaciones c
              JOIN Rrhh.Empleados e ON c.EmpleadoID = e.EmpleadoID
              WHERE c.FechaVencimiento IS NOT NULL
                AND c.FechaVencimiento > GETDATE()
                AND c.FechaVencimiento <= DATEADD(day, 30, GETDATE())");
        alertas.AddRange(capacitaciones);

        var ausencias = await conn.QueryAsync<AlertaRrhh>(
            @"SELECT
                'AUSENCIA_PENDIENTE' AS Tipo,
                a.Tipo + ' pendiente de aprobación' AS Descripcion,
                e.Nombres + ' ' + e.Apellidos AS Empleado,
                e.EmpleadoID,
                a.FechaCreacion AS FechaReferencia
              FROM Rrhh.Ausencias a
              JOIN Rrhh.Empleados e ON a.EmpleadoID = e.EmpleadoID
              WHERE a.Estado = 'PENDIENTE'");
        alertas.AddRange(ausencias);

        // Contratos por vencer: tabla nueva, tolerar si aún no existe en BD
        try
        {
            var contratos = await conn.QueryAsync<AlertaRrhh>(
                @"SELECT
                    'CONTRATO_VENCE' AS Tipo,
                    'Contrato ' + co.TipoContrato + ' vence el ' + CONVERT(NVARCHAR(10), co.FechaFin, 103) AS Descripcion,
                    e.Nombres + ' ' + e.Apellidos AS Empleado,
                    e.EmpleadoID,
                    CAST(co.FechaFin AS DATETIME2) AS FechaReferencia
                  FROM Rrhh.Contratos co
                  JOIN Rrhh.Empleados e ON co.EmpleadoID = e.EmpleadoID
                  WHERE co.Estado = 'ACTIVO' AND co.FechaFin IS NOT NULL
                    AND co.FechaFin > GETDATE()
                    AND co.FechaFin <= DATEADD(day, 30, GETDATE())");
            alertas.AddRange(contratos);
        }
        catch { /* tabla aún no migrada en esta BD */ }

        return Ok(alertas.OrderBy(a => a.FechaReferencia));
    }

    // ================= Export Excel empleados (oct 2026) =================

    [HttpGet("exportar/excel")]
    public async Task<IActionResult> ExportarExcel([FromServices] IDbConnectionFactory db)
    {
        using var conn = db.CreateConnection();
        var empleados = await conn.QueryAsync<EmpleadoExportRow>(
            @"SELECT e.EmpleadoID, e.Nombres, e.Apellidos,
                     ca.Nombre AS Cargo, d.Nombre AS Departamento,
                     cc.Nombre AS CentroCosto,
                     CONVERT(NVARCHAR(10), e.FechaIngreso, 103) AS FechaIngreso,
                     e.Telefono, e.Email,
                     CAST(CASE WHEN e.Estado = 1 THEN 1 ELSE 0 END AS BIT) AS Estado,
                     e.TarifaHora, e.TipoSangre
              FROM Rrhh.Empleados e
              LEFT JOIN Rrhh.Cargos ca ON e.CargoID = ca.CargoID
              LEFT JOIN Rrhh.Departamentos d ON ca.DepartamentoID = d.DepartamentoID
              LEFT JOIN Organizacion.CentrosCosto cc ON e.CentroCostoID = cc.CentroCostoID
              ORDER BY e.Apellidos, e.Nombres");
        var bytes = NexoApi.Common.Export.ExportService.GenerarExcelEmpleados(empleados);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"empleados-{NexoApi.Common.ColombiaClock.Now:yyyyMMdd}.xlsx");
    }
}
