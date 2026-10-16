using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NexoApi.Common.Security;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(int usuarioId, string username, string rol, int? centroCostoId)
    {
        var jwtSection = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, rol)
        };
        if (centroCostoId.HasValue)
            claims.Add(new Claim("CentroCostoId", centroCostoId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"], audience: jwtSection["Audience"], claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtSection["ExpirationMinutes"]!)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}