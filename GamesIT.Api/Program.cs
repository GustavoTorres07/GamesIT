using GamesIT.Api.Datos;
using GamesIT.Api.Modelos;
using GamesIT.Api.Servicios;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(opciones =>
    opciones.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();

var conexion = builder.Configuration.GetConnectionString("ConexionGamesIT")
    ?? throw new InvalidOperationException("No se encontró ConexionGamesIT.");

builder.Services.AddDbContext<ContextoGamesIT>(opciones => opciones.UseSqlServer(conexion));

builder.Services.AddIdentity<Usuario, IdentityRole<Guid>>(opciones =>
{
    opciones.User.RequireUniqueEmail = true;
    opciones.Password.RequiredLength = 8;
    opciones.Password.RequireDigit = true;
    opciones.Password.RequireLowercase = true;
    opciones.Password.RequireUppercase = true;
    opciones.Password.RequireNonAlphanumeric = false;
    opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    opciones.Lockout.MaxFailedAccessAttempts = 5;
    opciones.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ContextoGamesIT>()
.AddDefaultTokenProviders();

// En desarrollo: rechazo rápido de cookies de usuarios eliminados.
builder.Services.Configure<SecurityStampValidatorOptions>(opciones =>
    opciones.ValidationInterval = TimeSpan.Zero);

builder.Services.ConfigureApplicationCookie(opciones =>
{
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    opciones.Cookie.SameSite = SameSiteMode.Lax;
    opciones.ExpireTimeSpan = TimeSpan.FromDays(14);
    opciones.SlidingExpiration = true;

    opciones.Events.OnRedirectToLogin = contexto =>
    {
        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    opciones.Events.OnRedirectToAccessDenied = contexto =>
    {
        contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

const string politicaCors = "FrontendGamesIT";
string[] origenes = builder.Environment.IsDevelopment()
    ? ["https://localhost:3000", "http://localhost:3000"]
    : builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];

builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy(politicaCors, politica =>
    {
        politica.WithOrigins(origenes)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddAntiforgery(opciones =>
{
    opciones.HeaderName = "X-CSRF-TOKEN";
    opciones.Cookie.Name = ".GamesIT.Antiforgery";
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    opciones.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddRateLimiter(opciones =>
{
    opciones.AddPolicy("eliminacion", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    opciones.AddPolicy("registro", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    opciones.AddPolicy("verificacion", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    // NUEVO: protege el consumo facturable de OpenRouter por jugador.
    opciones.AddPolicy("generacion-ahorcado", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? contexto.Connection.RemoteIpAddress?.ToString()
                ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddScoped<ServicioCorreoSmtp>();
builder.Services.AddScoped<ServicioRegistroPorCorreo>();
builder.Services.AddScoped<ServicioAutenticacion>();
builder.Services.AddScoped<ServicioCuentas>();

// NUEVO: historial temporal de palabras por usuario para evitar duplicados.
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("OpenRouter", cliente =>
{
    cliente.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
    cliente.Timeout = TimeSpan.FromSeconds(60);
    cliente.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

builder.Services.AddScoped<ServicioGeneracionIA>();
builder.Services.AddHostedService<LimpiezaRegistrosPendientesService>();
builder.Services.AddScoped<ServicioPartidasAhorcado>();
builder.Services.AddHostedService<LimpiezaPartidasInactivasService>();
builder.Services.AddScoped<ServicioRecompensas>();


var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(politicaCors);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

await InicializadorAdministrador.InicializarAsync(
    app.Services, app.Configuration, app.Environment);

app.Run();
