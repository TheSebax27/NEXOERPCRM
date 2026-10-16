using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NexoApi.Common.Data;

namespace NexoApi.Common.Security;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly IDbConnectionFactory _db;

    private record AgenteInfo(int CentroCostoID, bool Activo);

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDbConnectionFactory db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var valores))
            return AuthenticateResult.Fail($"Falta el header {HeaderName}.");

        var apiKey = valores.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthenticateResult.Fail($"El header {HeaderName} esta vacio.");

        var apiKeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

        using var connection = _db.CreateConnection();
        var agente = await connection.QuerySingleOrDefaultAsync<AgenteInfo>(
            "SELECT CentroCostoID, Activo FROM Integracion.AgentesSync WHERE ApiKeyHash = @ApiKeyHash",
            new { ApiKeyHash = apiKeyHash });

        if (agente is null || !agente.Activo)
            return AuthenticateResult.Fail("API Key invalida o inactiva.");

        await connection.ExecuteAsync(
            "UPDATE Integracion.AgentesSync SET UltimaConexion = SYSUTCDATETIME() WHERE ApiKeyHash = @ApiKeyHash",
            new { ApiKeyHash = apiKeyHash });

        var claims = new[]
        {
            new Claim("CentroCostoId", agente.CentroCostoID.ToString()),
            new Claim(ClaimTypes.NameIdentifier, agente.CentroCostoID.ToString()),
            new Claim(ClaimTypes.Role, "Agente")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}