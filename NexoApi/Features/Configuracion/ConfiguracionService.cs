using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Configuracion.Dtos;

namespace NexoApi.Features.Configuracion;

public interface IConfiguracionService
{
    Task<ConfiguracionEmpresaResponse> ObtenerEmpresaAsync();
    Task ActualizarNombreEmpresaAsync(string nombreEmpresa, string? nombrePropietario);
    Task ActualizarLogoEmpresaAsync(string base64, string contentType);
    Task EliminarLogoEmpresaAsync();
    Task ActualizarUsaVisionsAsync(bool usaVisions);
    Task ActualizarConfigInventarioAsync(bool manejarVencimientos, int diasAlerta, string modoLotes);
    Task<ConfigNroDocResponse> ObtenerConfigNroDocAsync();
    Task ActualizarConfigNroDocAsync(string modoNroDoc, long ultimoSecuencial);
    Task ActualizarVentaDesdeRecetaAsync(bool ventaDesdeReceta);
    Task ActualizarSistemaMedidasAsync(bool sistemaMedidas);
    Task ActualizarVenderSinStockAsync(bool venderSinStock);
}

// Configuracion global de la empresa (nombre + logo del sidebar) -- a
// diferencia de Seguridad.PreferenciasUsuario, esto NO es por usuario: es
// una sola fila (ConfiguracionID = 1) que ve y usa todo el mundo, pero solo
// Administracion puede editarla (logica de rol en el controller).
public class ConfiguracionService : IConfiguracionService
{
    private readonly IDbConnectionFactory _db;

    public ConfiguracionService(IDbConnectionFactory db)
    {
        _db = db;
    }

    // Garantiza que ConfiguracionID = 1 exista para el tenant actual.
    // Todos los UPDATE dependen de esta fila; sin ella afectan 0 rows sin error.
    private static async Task EnsureConfigRowAsync(System.Data.IDbConnection connection)
    {
        await connection.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1)
                INSERT INTO Organizacion.ConfiguracionEmpresa
                    (ConfiguracionID, NombreEmpresa, NombrePropietario, Logo, LogoContentType,
                     UsaVisions, ManejarVencimientos, DiasAlertaVencimiento, ModoLotes,
                     ModoNroDoc, UltimoNroDocSecuencial, VentaDesdeReceta, SistemaMedidas, VenderSinStock)
                VALUES
                    (1, 'NEXO ERP', NULL, NULL, NULL,
                     1, 0, 7, 'FIFO',
                     'Manual', 0, 0, 0, 0)
            """);
    }

    public async Task<ConfiguracionEmpresaResponse> ObtenerEmpresaAsync()
    {
        using var connection = _db.CreateConnection();
        await EnsureConfigRowAsync(connection);

        var fila = await connection.QuerySingleAsync<(
            string NombreEmpresa, string? NombrePropietario,
            byte[]? Logo, string? LogoContentType, bool UsaVisions,
            bool ManejarVencimientos, int DiasAlertaVencimiento, string ModoLotes,
            string ModoNroDoc, long UltimoNroDocSecuencial, bool VentaDesdeReceta, bool SistemaMedidas, bool VenderSinStock)>(
            @"SELECT NombreEmpresa, NombrePropietario, Logo, LogoContentType, UsaVisions,
                     ManejarVencimientos, DiasAlertaVencimiento, ModoLotes,
                     ModoNroDoc, UltimoNroDocSecuencial, VentaDesdeReceta, SistemaMedidas,
                     ISNULL(VenderSinStock, 0) AS VenderSinStock
              FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");

        return new ConfiguracionEmpresaResponse(
            fila.NombreEmpresa, fila.NombrePropietario,
            fila.Logo is null ? null : Convert.ToBase64String(fila.Logo),
            fila.LogoContentType, fila.UsaVisions,
            fila.ManejarVencimientos, fila.DiasAlertaVencimiento, fila.ModoLotes,
            fila.ModoNroDoc, fila.UltimoNroDocSecuencial, fila.VentaDesdeReceta, fila.SistemaMedidas, fila.VenderSinStock);
    }

    public async Task ActualizarNombreEmpresaAsync(string nombreEmpresa, string? nombrePropietario)
    {
        using var connection = _db.CreateConnection();
        await EnsureConfigRowAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE Organizacion.ConfiguracionEmpresa SET NombreEmpresa = @NombreEmpresa, NombrePropietario = @NombrePropietario WHERE ConfiguracionID = 1",
            new { NombreEmpresa = nombreEmpresa, NombrePropietario = nombrePropietario });
    }

    // El UPDATE reemplaza el logo anterior -- no es un archivo en disco, es
    // una columna varbinary que simplemente se sobrescribe.
    public async Task ActualizarLogoEmpresaAsync(string base64, string contentType)
    {
        using var connection = _db.CreateConnection();
        await EnsureConfigRowAsync(connection);
        var datos = Convert.FromBase64String(base64);

        await connection.ExecuteAsync(
            "UPDATE Organizacion.ConfiguracionEmpresa SET Logo = @Datos, LogoContentType = @ContentType WHERE ConfiguracionID = 1",
            new { Datos = datos, ContentType = contentType });
    }

    public async Task EliminarLogoEmpresaAsync()
    {
        using var connection = _db.CreateConnection();
        await EnsureConfigRowAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE Organizacion.ConfiguracionEmpresa SET Logo = NULL, LogoContentType = NULL WHERE ConfiguracionID = 1");
    }

    public async Task ActualizarUsaVisionsAsync(bool usaVisions)
    {
        using var connection = _db.CreateConnection();
        await EnsureConfigRowAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE Organizacion.ConfiguracionEmpresa SET UsaVisions = @UsaVisions WHERE ConfiguracionID = 1",
            new { UsaVisions = usaVisions });
    }

    public async Task ActualizarConfigInventarioAsync(bool manejarVencimientos, int diasAlerta, string modoLotes)
    {
        if (modoLotes != "FIFO" && modoLotes != "MANUAL")
            throw new ArgumentException("ModoLotes debe ser FIFO o MANUAL.");
        if (diasAlerta < 1 || diasAlerta > 365)
            throw new ArgumentException("DiasAlertaVencimiento debe estar entre 1 y 365.");

        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE Organizacion.ConfiguracionEmpresa
              SET ManejarVencimientos = @ManejarVencimientos,
                  DiasAlertaVencimiento = @DiasAlertaVencimiento,
                  ModoLotes = @ModoLotes
              WHERE ConfiguracionID = 1",
            new { ManejarVencimientos = manejarVencimientos, DiasAlertaVencimiento = diasAlerta, ModoLotes = modoLotes });
    }

    public async Task<ConfigNroDocResponse> ObtenerConfigNroDocAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QuerySingleAsync<ConfigNroDocResponse>(
            "SELECT ModoNroDoc, UltimoNroDocSecuencial FROM Organizacion.ConfiguracionEmpresa WHERE ConfiguracionID = 1");
    }

    public async Task ActualizarConfigNroDocAsync(string modoNroDoc, long ultimoSecuencial)
    {
        var modosValidos = new[] { "Secuencial", "Aleatorio", "Manual" };
        if (!modosValidos.Contains(modoNroDoc))
            throw new ArgumentException("ModoNroDoc invalido.");

        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE Organizacion.ConfiguracionEmpresa
              SET ModoNroDoc = @ModoNroDoc, UltimoNroDocSecuencial = @UltimoNroDocSecuencial
              WHERE ConfiguracionID = 1",
            new { ModoNroDoc = modoNroDoc, UltimoNroDocSecuencial = ultimoSecuencial });
    }

    public async Task ActualizarVentaDesdeRecetaAsync(bool ventaDesdeReceta)
    {
        using var connection = _db.CreateConnection();
        await EnsureConfigRowAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE Organizacion.ConfiguracionEmpresa SET VentaDesdeReceta = @VentaDesdeReceta WHERE ConfiguracionID = 1",
            new { VentaDesdeReceta = ventaDesdeReceta });
    }

    public async Task ActualizarSistemaMedidasAsync(bool sistemaMedidas)
    {
        using var connection = _db.CreateConnection();
        await EnsureConfigRowAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE Organizacion.ConfiguracionEmpresa SET SistemaMedidas = @SistemaMedidas WHERE ConfiguracionID = 1",
            new { SistemaMedidas = sistemaMedidas });
    }

    public async Task ActualizarVenderSinStockAsync(bool venderSinStock)
    {
        using var connection = _db.CreateConnection();
        await EnsureConfigRowAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE Organizacion.ConfiguracionEmpresa SET VenderSinStock = @VenderSinStock WHERE ConfiguracionID = 1",
            new { VenderSinStock = venderSinStock });
    }
}
