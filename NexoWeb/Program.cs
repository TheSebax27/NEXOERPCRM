using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using NexoWeb.Common.Auth;
using MudBlazor.Services;
using NexoWeb.Common.ApiClient;
using NexoWeb.Components;

var builder = WebApplication.CreateBuilder(args);

// Cultura colombiana global: hace que TODOS los ToString("C2")/("N2") de la app
// (tablas, chips, totales) muestren "$1.234.567,89" -- punto como separador de
// miles, coma como decimal -- sin tener que tocar cada pantalla una por una.
var culturaCO = new CultureInfo("es-CO");
CultureInfo.DefaultThreadCurrentCulture = culturaCO;
CultureInfo.DefaultThreadCurrentUICulture = culturaCO;

// Blazor Server: los componentes .razor + la conexion en tiempo real (SignalR)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o =>
    {
        // Retiene el circuito 10 min en lugar de los 3 min por defecto,
        // para que una pausa breve no corte la sesión.
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
    })
    .AddHubOptions(o =>
    {
        // Permite subir imágenes base64 (~5 MB) a través del canal SignalR
        o.MaximumReceiveMessageSize = 10 * 1024 * 1024;
        // Keepalive más agresivo para detectar desconexiones antes (default 15s)
        o.KeepAliveInterval = TimeSpan.FromSeconds(10);
        // Si en 30 s no hay respuesta del cliente, cierra el circuito
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    });

// MudBlazor
builder.Services.AddMudServices();

// Reenvía el Host del request del usuario al API para resolución multi-tenant
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TenantHostForwardingHandler>();

// El cliente HTTP hacia NexoApi, con su URL base ya configurada
builder.Services.AddHttpClient<INexoApiClient, NexoApiClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["NexoApi:BaseUrl"]!);
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddHttpMessageHandler<TenantHostForwardingHandler>();

// Cliente sin-auth para proxy de imágenes (los endpoints de imagen son AllowAnonymous en la API)
builder.Services.AddHttpClient("img-proxy").ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });

// Cliente para proxear el track/click de marketing: NO sigue redirects para poder
// devolverlos al browser del destinatario del email.
builder.Services.AddHttpClient("no-redirect-proxy").ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        AllowAutoRedirect = false
    });

builder.Services.AddAuthorizationCore(); // el "motor" de autorizacion de Blazor (distinto al AddAuthorization de la API)
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<PreferenciasState>();
builder.Services.AddScoped<ConfiguracionEmpresaState>();
builder.Services.AddScoped<ModulosVisiblesState>();
builder.Services.AddSingleton<CalendarioBroadcast>();

var app = builder.Build();

// Fuerza es-CO en cada request (Blazor Server corre cada circuito en su propio
// contexto, asi que DefaultThreadCurrentCulture solo no basta para todos los casos).
var opcionesLocalizacion = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions()
    .SetDefaultCulture("es-CO")
    .AddSupportedCultures("es-CO")
    .AddSupportedUICultures("es-CO");
app.UseRequestLocalization(opcionesLocalizacion);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// El HTML shell de Blazor debe llegar siempre fresco: si el browser cachea el HTML
// con referencias a assets viejos (tras cambio de dominio o deploy) la app no carga.
// Solo afecta text/html — los assets estáticos los maneja MapStaticAssets() aparte.
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        if (ctx.Response.ContentType is string ct &&
            ct.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.Headers["Cache-Control"] = "no-store, must-revalidate";
            ctx.Response.Headers["Pragma"] = "no-cache";
        }
        return Task.CompletedTask;
    });
    await next();
});

app.MapStaticAssets(); // fingerprinting + cache-busting automático para app.css / NexoWeb.styles.css
app.UseAntiforgery();

// ── Proxy de imágenes hacia NexoApi ──────────────────────────────────────────
// Los <img src="api/..."> del browser no pueden mandar el JWT.
// NexoWeb recibe la petición y la reenvía al API (cuyos endpoints de imagen ya son AllowAnonymous).
{
    var apiBase = app.Configuration["NexoApi:BaseUrl"]!.TrimEnd('/');

    static async Task<IResult> ProxyImagen(string apiUrl, IHttpClientFactory hf, string? tenantHost = null)
    {
        var client = hf.CreateClient("img-proxy");
        var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        if (!string.IsNullOrEmpty(tenantHost))
            req.Headers.TryAddWithoutValidation("X-Nexo-Host", tenantHost);
        HttpResponseMessage resp;
        try { resp = await client.SendAsync(req); }
        catch { return Results.NotFound(); }
        if (!resp.IsSuccessStatusCode) return Results.NotFound();
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var mime  = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        return Results.File(bytes, mime);
    }

    app.MapGet("api/catalogo/articulos/{id:int}/imagen",
        (int id, HttpContext ctx, IHttpClientFactory hf) =>
            ProxyImagen($"{apiBase}/api/catalogo/articulos/{id}/imagen", hf, ctx.Request.Host.Host));

    app.MapGet("api/rrhh/empleados/{id:int}/foto",
        (int id, HttpContext ctx, IHttpClientFactory hf) =>
            ProxyImagen($"{apiBase}/api/rrhh/empleados/{id}/foto", hf, ctx.Request.Host.Host));

    app.MapGet("api/marketing/combos/{id:int}/imagen",
        (int id, HttpContext ctx, IHttpClientFactory hf) =>
            ProxyImagen($"{apiBase}/api/marketing/combos/{id}/imagen", hf, ctx.Request.Host.Host));

    app.MapGet("api/produccion/maquinaria/{id:int}/foto",
        (int id, HttpContext ctx, IHttpClientFactory hf) =>
            ProxyImagen($"{apiBase}/api/produccion/maquinaria/{id}/foto", hf, ctx.Request.Host.Host));

    app.MapGet("api/rrhh/asistencia/qr-imagen",
        async (HttpContext ctx, IHttpClientFactory hf) =>
            await ProxyImagen($"{apiBase}/api/rrhh/asistencia/qr-imagen{ctx.Request.QueryString}", hf, ctx.Request.Host.Host));

    // ── Proxy de tracking de marketing (los links van en emails → deben pasar por el Web del tenant)
    app.MapGet("api/marketing/track/open/{token}",
        async (string token, HttpContext ctx, IHttpClientFactory hf) =>
            await ProxyImagen($"{apiBase}/api/marketing/track/open/{token}", hf, ctx.Request.Host.Host));

    app.MapGet("api/marketing/track/click/{token}",
        async (string token, HttpContext ctx, IHttpClientFactory hf) =>
        {
            var client = hf.CreateClient("no-redirect-proxy");
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{apiBase}/api/marketing/track/click/{token}{ctx.Request.QueryString}");
            req.Headers.TryAddWithoutValidation("X-Nexo-Host", ctx.Request.Host.Host);
            HttpResponseMessage resp;
            try { resp = await client.SendAsync(req); }
            catch { return Results.NotFound(); }
            var location = resp.Headers.Location?.ToString();
            return !string.IsNullOrEmpty(location) ? Results.Redirect(location) : Results.NotFound();
        });

    app.MapGet("api/marketing/unsub/{token}",
        async (string token, HttpContext ctx, IHttpClientFactory hf) =>
        {
            var client = hf.CreateClient("img-proxy");
            var req = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/api/marketing/unsub/{token}");
            req.Headers.TryAddWithoutValidation("X-Nexo-Host", ctx.Request.Host.Host);
            HttpResponseMessage resp;
            try { resp = await client.SendAsync(req); }
            catch { return Results.StatusCode(502); }
            var html = await resp.Content.ReadAsStringAsync();
            return Results.Content(html, "text/html");
        });

    app.MapGet("api/integracion/descargar-agente/{token}",
        async (string token, HttpContext ctx, IHttpClientFactory hf) =>
        {
            var client = hf.CreateClient("img-proxy");
            var req = new HttpRequestMessage(HttpMethod.Get, $"{apiBase}/api/integracion/descargar-agente/{token}");
            req.Headers.TryAddWithoutValidation("X-Nexo-Host", ctx.Request.Host.Host);
            HttpResponseMessage resp;
            try { resp = await client.SendAsync(req); }
            catch { return Results.NotFound(); }
            if (!resp.IsSuccessStatusCode) return Results.NotFound();
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            return Results.File(bytes, "application/octet-stream", "NexoAgente-Setup.exe");
        });
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous(); // evita que ASP.NET Core exija un IAuthenticationService real a nivel de endpoint;
                       // la autorizacion queda 100% a cargo de AuthorizeRouteView + CustomAuthStateProvider

app.Run();