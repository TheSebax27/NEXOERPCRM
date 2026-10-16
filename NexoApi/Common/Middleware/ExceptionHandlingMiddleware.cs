using Microsoft.Data.SqlClient;
using System.Data.SqlClient;
using System.Net;
using System.Text.Json;
using SqlException = Microsoft.Data.SqlClient.SqlException;

namespace NexoApi.Common.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (SqlException ex) when (ex.Number is >= 50000 and < 60000)
        {
            _logger.LogWarning(ex, "Regla de negocio rechazada por la base de datos");
            // 51020/51040/51050 = registro no encontrado → 404
            if (ex.Number is 51020 or 51040 or 51050)
            {
                await WriteResponse(context, HttpStatusCode.NotFound, ex.Message);
                return;
            }
            // 51001/51002/51010/51011/51023/51030/51041/51051/52001/53000/53010 = estado invalido o conflicto → 409
            // 51021/51022/52000/54000/resto = solicitud mal formada → 400
            var esConflicto = ex.Number is 51001 or 51002 or 51010 or 51011 or 51023 or 51030 or 51041 or 51051
                                           or 52001 or 53000 or 53010;
            var statusCode = esConflicto ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest;
            await WriteResponse(context, statusCode, ex.Message);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            _logger.LogWarning(ex, "Violacion de restriccion UNIQUE");
            await WriteResponse(context, HttpStatusCode.Conflict,
                "Ya existe un registro con ese valor. Verifique campos unicos (codigo, SKU, NIT, nombre, etc.).");
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            _logger.LogWarning(ex, "Violacion de FK");
            await WriteResponse(context, HttpStatusCode.BadRequest,
                "No se puede completar la operacion: uno de los valores referenciados no existe o tiene registros dependientes.");
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error de base de datos no controlado");
            await WriteResponse(context, HttpStatusCode.InternalServerError, "Ocurrio un error interno al procesar la operacion.");
        }
        catch (KeyNotFoundException ex)
        {
            await WriteResponse(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado");
            await WriteResponse(context, HttpStatusCode.InternalServerError, "Ocurrio un error interno inesperado.");
        }
    }

    private static async Task WriteResponse(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}