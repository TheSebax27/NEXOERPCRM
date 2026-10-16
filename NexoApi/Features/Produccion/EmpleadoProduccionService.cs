using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Produccion.Dtos;

namespace NexoApi.Features.Produccion;

public interface IEmpleadoProduccionService
{
    Task<IEnumerable<EmpleadoRecetaItem>> ListarEmpleadosRecetaAsync(int recetaId);
    Task GuardarEmpleadosRecetaAsync(int recetaId, List<EmpleadoRecetaInput> empleados);

    Task<IEnumerable<EmpleadoOrdenItem>> ListarEmpleadosOrdenAsync(int ordenId);
    Task GuardarEmpleadosOrdenAsync(int ordenId, List<EmpleadoOrdenInput> empleados);
}

public class EmpleadoProduccionService(IDbConnectionFactory db) : IEmpleadoProduccionService
{
    public async Task<IEnumerable<EmpleadoRecetaItem>> ListarEmpleadosRecetaAsync(int recetaId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<EmpleadoRecetaItem>("""
            SELECT e.EmpleadoID, e.Nombres, e.Apellidos, c.Nombre AS Cargo,
                   e.TarifaHora, re.HorasEstimadasPorLote, re.Notas
            FROM Produccion.RecetaEmpleado re
            JOIN Rrhh.Empleados e      ON e.EmpleadoID = re.EmpleadoID
            LEFT JOIN Rrhh.Cargos c   ON c.CargoID    = e.CargoID
            WHERE re.RecetaID = @recetaId
            ORDER BY e.Nombres, e.Apellidos
            """, new { recetaId });
    }

    public async Task GuardarEmpleadosRecetaAsync(int recetaId, List<EmpleadoRecetaInput> empleados)
    {
        using var conn = db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(
            "DELETE FROM Produccion.RecetaEmpleado WHERE RecetaID = @recetaId",
            new { recetaId }, tx);
        if (empleados.Count > 0)
        {
            const string sql = """
                INSERT INTO Produccion.RecetaEmpleado (RecetaID, EmpleadoID, HorasEstimadasPorLote, Notas)
                VALUES (@RecetaID, @EmpleadoID, @HorasEstimadasPorLote, @Notas)
                """;
            foreach (var emp in empleados)
                await conn.ExecuteAsync(sql,
                    new { RecetaID = recetaId, emp.EmpleadoID, emp.HorasEstimadasPorLote, emp.Notas }, tx);
        }
        tx.Commit();
    }

    public async Task<IEnumerable<EmpleadoOrdenItem>> ListarEmpleadosOrdenAsync(int ordenId)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<EmpleadoOrdenItem>("""
            SELECT e.EmpleadoID, e.Nombres, e.Apellidos, c.Nombre AS Cargo,
                   e.TarifaHora, oe.HorasReales, oe.Notas
            FROM Produccion.OrdenEmpleado oe
            JOIN Rrhh.Empleados e      ON e.EmpleadoID = oe.EmpleadoID
            LEFT JOIN Rrhh.Cargos c   ON c.CargoID    = e.CargoID
            WHERE oe.OrdenProduccionID = @ordenId
            ORDER BY e.Nombres, e.Apellidos
            """, new { ordenId });
    }

    public async Task GuardarEmpleadosOrdenAsync(int ordenId, List<EmpleadoOrdenInput> empleados)
    {
        using var conn = db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(
            "DELETE FROM Produccion.OrdenEmpleado WHERE OrdenProduccionID = @ordenId",
            new { ordenId }, tx);
        if (empleados.Count > 0)
        {
            const string sql = """
                INSERT INTO Produccion.OrdenEmpleado (OrdenProduccionID, EmpleadoID, HorasReales, Notas)
                VALUES (@OrdenProduccionID, @EmpleadoID, @HorasReales, @Notas)
                """;
            foreach (var emp in empleados)
                await conn.ExecuteAsync(sql,
                    new { OrdenProduccionID = ordenId, emp.EmpleadoID, emp.HorasReales, emp.Notas }, tx);
        }
        tx.Commit();
    }
}
