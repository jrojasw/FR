using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Abstractions;
using PrecioHerramientas.Infrastructure.Persistence;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Jobs;

namespace PrecioHerramientas.Web.Infrastructure;

public static class StartupTasks
{
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        // En docker-compose la base puede tardar unos segundos más que la web en aceptar conexiones.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("Migraciones aplicadas");
                return;
            }
            catch (Exception ex) when (attempt < 10 && ex is Npgsql.NpgsqlException or TimeoutException)
            {
                logger.LogWarning("Base de datos no disponible (intento {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    /// <summary>
    /// Registra en la base las tiendas habilitadas y crea/actualiza sus trabajos
    /// recurrentes en Hangfire según el cron configurado. Las deshabilitadas se quitan.
    /// </summary>
    public static async Task ConfigureScrapingScheduleAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var scraperOptions = services.GetRequiredService<IOptions<ScraperOptions>>().Value;
        var hangfire = services.GetRequiredService<IOptions<HangfireSettings>>().Value;
        var recurringJobs = services.GetRequiredService<IRecurringJobManager>();
        var stores = services.GetRequiredService<IStoreRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var logger = services.GetRequiredService<ILogger<ScrapeJob>>();
        var timeZone = ResolveTimeZone(hangfire.TimeZone, logger);

        RemoveObsoleteJobs(recurringJobs, services.GetServices<IStoreScraper>().Select(s => $"scrape-{s.Store.Key}").ToHashSet(), logger);

        foreach (var scraper in services.GetServices<IStoreScraper>())
        {
            var key = scraper.Store.Key;
            var jobId = $"scrape-{key}";
            var options = scraperOptions.For(key);
            if (!options.Enabled)
            {
                recurringJobs.RemoveIfExists(jobId);
                continue;
            }

            await ScrapeJob.EnsureStoreAsync(stores, unitOfWork, scraper.Store, CancellationToken.None);
            recurringJobs.AddOrUpdate<ScrapeJob>(
                jobId,
                "scraping",
                job => job.RunAsync(key, CancellationToken.None),
                options.Cron,
                new RecurringJobOptions { TimeZone = timeZone });
            logger.LogInformation("Tienda {Store} programada con cron '{Cron}'", key, options.Cron);
        }
    }

    /// <summary>Trabajos de tiendas que ya no existen y que Hangfire conserva en su base.</summary>
    private static readonly string[] RetiredJobIds = ["scrape-demo-norte", "scrape-demo-sur"];

    /// <summary>
    /// Quita de Hangfire los trabajos "scrape-*" sin scraper registrado (tiendas retiradas,
    /// como las demo): si no, Hangfire los seguiría ejecutando y fallarían.
    /// </summary>
    private static void RemoveObsoleteJobs(IRecurringJobManager recurringJobs, IReadOnlySet<string> currentJobIds, ILogger logger)
    {
        var stored = Enumerable.Empty<string>();
        try
        {
            using var connection = JobStorage.Current.GetConnection();
            stored = connection.GetRecurringJobs().Select(j => j.Id).Where(id => id.StartsWith("scrape-", StringComparison.Ordinal)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo listar los trabajos recurrentes de Hangfire");
        }

        foreach (var jobId in RetiredJobIds.Concat(stored).Where(id => !currentJobIds.Contains(id)).Distinct())
        {
            recurringJobs.RemoveIfExists(jobId);
            if (stored.Contains(jobId))
                logger.LogInformation("Trabajo recurrente {JobId} eliminado: su tienda ya no existe", jobId);
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string id, ILogger logger)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            logger.LogWarning("Zona horaria {TimeZone} no encontrada; se usa UTC", id);
            return TimeZoneInfo.Utc;
        }
    }
}
