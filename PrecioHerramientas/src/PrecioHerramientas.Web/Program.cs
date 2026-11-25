using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core;
using PrecioHerramientas.Core.Affiliates;
using PrecioHerramientas.Core.Queries;
using PrecioHerramientas.Core.Services;
using PrecioHerramientas.Infrastructure;
using PrecioHerramientas.Infrastructure.Persistence;
using PrecioHerramientas.Scrapers;
using PrecioHerramientas.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Railway, Render y otros indican en PORT el puerto donde debe escuchar la app.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddRazorPages();
builder.Services.AddCore();
builder.Services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));
builder.Services.Configure<AffiliateOptions>(configuration.GetSection(AffiliateOptions.SectionName));
builder.Services.AddInfrastructure(configuration);
builder.Services.AddScrapers(configuration);
// Las claves de Data Protection (antiforgery del dashboard de Hangfire) se guardan en
// PostgreSQL: en Railway/Docker el disco del contenedor se pierde en cada despliegue.
builder.Services.AddDataProtection()
    .SetApplicationName("PrecioHerramientas")
    .PersistKeysToDbContext<AppDbContext>();

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

// Hangfire guarda sus colas en el mismo PostgreSQL, en el esquema "hangfire".
var hangfireSettings = configuration.GetSection(HangfireSettings.SectionName).Get<HangfireSettings>() ?? new HangfireSettings();
builder.Services.Configure<HangfireSettings>(configuration.GetSection(HangfireSettings.SectionName));
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseFilter(new AutomaticRetryAttribute { Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Fail })
    .UsePostgreSqlStorage(
        options => options.UseNpgsqlConnection(ConnectionStrings.Resolve(configuration)),
        new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));

if (hangfireSettings.RunServer)
{
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = hangfireSettings.WorkerCount;
        options.Queues = ["scraping", "default"];
    });
}

// Detrás de Caddy/nginx: confiar en X-Forwarded-* para conocer el esquema (https) y la IP real.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/NotFound", "?code={0}");
app.UseStaticFiles();
app.UseRouting();

// Panel de administración (/admin/...): mismo usuario y clave que el dashboard de Hangfire.
app.UseWhen(http => http.Request.Path.StartsWithSegments("/admin"), admin => admin.Use(async (http, next) =>
{
    if (!BasicAuth.IsAuthorized(http, hangfireSettings.DashboardUser, hangfireSettings.DashboardPassword, "Administracion"))
    {
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    await next(http);
}));

app.MapRazorPages();
app.MapHealthChecks("/health");

app.MapHangfireDashboard(hangfireSettings.DashboardPath, new DashboardOptions
{
    DashboardTitle = "PrecioHerramientas · trabajos",
    Authorization = [new HangfireBasicAuthFilter(hangfireSettings.DashboardUser, hangfireSettings.DashboardPassword)],
    AppPath = "/",
});

// Salida hacia las tiendas: registra el clic y redirige (302) al link de afiliado de la tienda.
// Solo se redirige a URLs guardadas por los scrapers, nunca a una dirección recibida en la petición.
var outbound = app.MapGroup("/ir");
outbound.MapGet("/{id:int}", async (int id, HttpContext http, ClickRedirectService clicks, CancellationToken ct) =>
    await clicks.ForOfferAsync(id, Visitor(http), ct) is { } url ? Outbound(http, url) : Results.NotFound());
outbound.MapGet("/tienda/{key}", async (string key, HttpContext http, ClickRedirectService clicks, CancellationToken ct) =>
    await clicks.ForStoreAsync(key, Visitor(http), ct) is { } url ? Outbound(http, url) : Results.NotFound());

static ClickVisitor Visitor(HttpContext http) => new(
    http.Connection.RemoteIpAddress,
    http.Request.Headers.Referer.ToString() is { Length: > 0 } referer ? referer : null,
    http.Request.Headers.UserAgent.ToString());

static IResult Outbound(HttpContext http, string url)
{
    // Cada clic debe llegar al servidor: sin caché, y fuera de los índices de buscadores.
    http.Response.Headers.CacheControl = "no-store";
    http.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    return Results.Redirect(url, permanent: false);
}

var api = app.MapGroup("/api");
api.MapGet("/productos", async (string? q, int? pagina, ICatalogQueries queries, CancellationToken ct) =>
    await queries.SearchAsync(new ProductSearchQuery { Text = q, Page = pagina ?? 1, PageSize = 24 }, ct));
api.MapGet("/productos/{id:int}/historial", async (int id, int? dias, ICatalogQueries queries, CancellationToken ct) =>
    await queries.GetPriceHistoryAsync(id, dias ?? 90, ct));

if (configuration.GetValue("Database:MigrateOnStartup", true))
    await app.MigrateDatabaseAsync();

await app.ConfigureScrapingScheduleAsync();

if (string.IsNullOrEmpty(hangfireSettings.DashboardPassword))
    app.Logger.LogWarning("Hangfire:DashboardPassword no está configurado: el dashboard {Path} queda bloqueado", hangfireSettings.DashboardPath);

app.Run();

/// <summary>Expuesto para pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
