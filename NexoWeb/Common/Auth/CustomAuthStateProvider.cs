using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NexoWeb.Common.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthStateService _authState;

    public CustomAuthStateProvider(AuthStateService authState)
    {
        _authState = authState;
        _authState.OnChange += () => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_authState.EstaLogueado)
        {
            var anonimo = new ClaimsPrincipal(new ClaimsIdentity());
            return Task.FromResult(new AuthenticationState(anonimo));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _authState.NombreCompleto!),
            new(ClaimTypes.Role, _authState.Rol!)
        };

        if (_authState.CentroCostoId.HasValue)
            claims.Add(new Claim("CentroCostoId", _authState.CentroCostoId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, authenticationType: "NexoAuth");
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult(new AuthenticationState(principal));
    }
}