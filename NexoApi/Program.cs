using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using NexoApi.Common.Data;
using NexoApi.Common.Middleware;
using NexoApi.Common.Security;
using NexoApi.Features.Auditoria;
using NexoApi.Features.Auth;
using NexoApi.Features.Busqueda;
using NexoApi.Features.Calendario;
using NexoApi.Features.Catalogo;
using NexoApi.Features.Compras;
using NexoApi.Features.Configuracion;
using NexoApi.Features.Crm;
using NexoApi.Features.Dashboard;
using NexoApi.Features.Facturacion;
using NexoApi.Features.Integracion;
using NexoApi.Features.Inventario;
using NexoApi.Features.Logistica;
using NexoApi.Features.Notificaciones;
using NexoApi.Features.Marketing;
using NexoApi.Features.Planificacion;
using NexoApi.Features.Preferencias;
using NexoApi.Features.Produccion;
using NexoApi.Features.Proyectos;
using NexoApi.Features.Recetas;
using NexoApi.Features.Rrhh;
using NexoApi.Features.Seguridad;
using NexoApi.Features.Traspasos;
using NexoApi.BackgroundServices;
using System.Text;
using Dapper;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/nexoapi-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// QuestPDF exige declarar el tipo de licencia antes de generar cualquier PDF.
// Community es gratuita para empresas con ingresos anuales menores a 1M USD
// (ver https://www.questpdf.com/license/) -- si el negocio crece mas alla de
// ese umbral, hay que comprar la licencia comercial de QuestPDF.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ----------------------------------------------------------------------------
// CONFIGURACIÓN DE SWAGGER (JWT + X-Api-Key)
// ----------------------------------------------------------------------------
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "NEXO API", Version = "v1" });

    // Evita colisión de SchemaId cuando dos namespaces distintos usan el mismo nombre de clase
    options.CustomSchemaIds(t => t.FullName?.Replace('+', '.') ?? t.Name);

    // Security definition para JWT (Usuarios)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Escribe: Bearer {tu token}"
    });

    // Security definition para API Key (Agente Sync)
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationHandler.HeaderName, // X-Api-Key
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Escribe tu API Key del Agente directamente"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() },
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } }, Array.Empty<string>() }
    });
});

// ----------------------------------------------------------------------------
// INFRAESTRUCTURA Y MÓDULOS DE NEGOCIO
// ----------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<TenantConnectionService>();
builder.Services.AddScoped<IDbConnectionFactory, TenantSqlConnectionFactory>();
builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOrdenesProduccionService, OrdenesProduccionService>();
builder.Services.AddScoped<IInventarioService, InventarioService>();
builder.Services.AddScoped<IOrdenesCompraService, OrdenesCompraService>();
builder.Services.AddScoped<ITraspasosService, TraspasosService>();
builder.Services.AddScoped<ICatalogoService, CatalogoService>();
builder.Services.AddScoped<IAdicionalesService, AdicionalesService>();
builder.Services.AddScoped<IRecetasService, RecetasService>();
builder.Services.AddScoped<IIntegracionService, IntegracionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IDashboardExportService, DashboardExportService>();
builder.Services.AddScoped<IRrhhService, RrhhService>();
builder.Services.AddScoped<CarnetEmpleadoService>();
builder.Services.AddScoped<NexoApi.Features.Facturacion.FacturaPdfService>();
builder.Services.AddScoped<NexoApi.Features.Compras.OrdenCompraPdfService>();
builder.Services.AddScoped<NexoApi.Features.Compras.EntradaVisionsPdfService>();
builder.Services.AddScoped<NexoApi.Features.Integracion.VentaVisionsPdfService>();
builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();
builder.Services.AddScoped<IAutomacionService, AutomacionService>();
builder.Services.AddScoped<ICrmService, CrmService>();
builder.Services.AddScoped<NexoApi.Features.Email.IEmailService, NexoApi.Features.Email.GmailEmailService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<NexoApi.Features.Chatbot.IChatbotService, NexoApi.Features.Chatbot.ChatbotService>();
builder.Services.AddHostedService<NexoApi.Infrastructure.AutomationBackgroundService>();
builder.Services.AddHostedService<NexoScheduler>();
builder.Services.AddHostedService<NexoApi.Common.BackgroundServices.AuditoriaPurgeService>();
builder.Services.AddScoped<IPlanificacionService, PlanificacionService>();
builder.Services.AddScoped<ICalendarioService, CalendarioService>();
builder.Services.AddScoped<ILogisticaService, LogisticaService>();
builder.Services.AddScoped<IProyectosService, ProyectosService>();
builder.Services.AddScoped<IFacturacionService, FacturacionService>();
builder.Services.AddScoped<IDevolucionService, DevolucionService>();
builder.Services.AddScoped<INotificacionesService, NotificacionesService>();
builder.Services.AddScoped<IBusquedaService, BusquedaService>();
builder.Services.AddScoped<IPreferenciasService, PreferenciasService>();
builder.Services.AddScoped<IConfiguracionService, ConfiguracionService>();
builder.Services.AddScoped<IMarketingService, MarketingService>();
builder.Services.AddScoped<IMaquinariaService, MaquinariaService>();
builder.Services.AddScoped<IEmpleadoProduccionService, EmpleadoProduccionService>();
builder.Services.AddScoped<IComboService, ComboService>();
builder.Services.AddScoped<ISeguridadService, SeguridadService>();
builder.Services.AddScoped<NexoApi.Features.Finanzas.IFinanzasService, NexoApi.Features.Finanzas.FinanzasService>();
builder.Services.AddScoped<NexoApi.Features.Soporte.ISoporteService, NexoApi.Features.Soporte.SoporteService>();
builder.Services.AddScoped<NexoApi.Features.Formularios.IFormulariosService, NexoApi.Features.Formularios.FormulariosService>();
builder.Services.AddScoped<NexoApi.Features.Conocimiento.IConocimientoService, NexoApi.Features.Conocimiento.ConocimientoService>();
builder.Services.AddScoped<NexoApi.Features.Reportes.IReportesService, NexoApi.Features.Reportes.ReportesService>();
builder.Services.AddHttpClient<NexoApi.Features.WhatsApp.IWhatsAppService, NexoApi.Features.WhatsApp.WhatsAppService>();
builder.Services.AddScoped<NexoApi.Features.Documentos.IDocumentosService, NexoApi.Features.Documentos.DocumentosService>();
builder.Services.AddScoped<NexoApi.Features.Comisiones.IComisionesService, NexoApi.Features.Comisiones.ComisionesService>();

// ----------------------------------------------------------------------------
// AUTENTICACIÓN Y AUTORIZACIÓN (AQUÍ ESTÁ EL CAMBIO)
// ----------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("Jwt");

// Verificacion minima: la clave JWT debe tener al menos 32 caracteres.
// La misma clave se usa para todos los clientes (multi-tenant, clave compartida).
if (string.IsNullOrWhiteSpace(jwtSection["Key"]) || jwtSection["Key"]!.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key debe tener al menos 32 caracteres. Revisa appsettings.json o appsettings.Production.json.");

builder.Services.AddCors(opts => opts.AddPolicy("PublicUtils",
    p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!))
    };
})
// <-- AQUÍ SE ENCADENA EL NUEVO ESQUEMA DE API KEY PARA EL AGENTE -->
.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationHandler.SchemeName,
    _ => { }
);

builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

// ----------------------------------------------------------------------------
// PIPELINE DE LA APLICACIÓN
// ----------------------------------------------------------------------------
var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseCors("PublicUtils");
app.UseMiddleware<ExceptionHandlingMiddleware>();
// TenantMiddleware debe ir ANTES de UseAuthentication porque ApiKeyAuthenticationHandler
// (usado por el agente) tambien necesita la conexion al tenant para validar la ApiKey
// contra Integracion.AgentesSync. Si va despues, CreateConnection() falla sin connStr.
app.UseMiddleware<TenantMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditoriaMiddleware>();
app.MapControllers();

app.Run();