using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PrecioHerramientas.Core.Abstractions;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Services;
using PrecioHerramientas.Scrapers.Abstractions;

namespace PrecioHerramientas.Scrapers.Jobs;

/// <summary>
/// Ejecuta el scraper de una tienda, guarda los resultados y deja registro en
/// <see cref="ScrapeRun"/>. Lo invoca Hangfire según el cron de cada tienda.
/// </summary>
public sealed class ScrapeJob(
    IEnumerable<IStoreScraper> scrapers,
    IStoreRepository stores,
    IScrapeRunRepository runs,
    IUnitOfWork unitOfWork,
    PriceIngestionService ingestion,
    IClock clock,
    ILogger<ScrapeJob> logger)
{
    // Evita dos ejecuciones simultáneas de la misma tienda (p. ej. manual + programada).
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

    public async Task RunAsync(string storeKey, CancellationToken ct = default)
    {
        var scraper = scrapers.FirstOrDefault(s => string.Equals(s.Store.Key, storeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No existe un scraper para la tienda '{storeKey}'.");

        var gate = Locks.GetOrAdd(scraper.Store.Key, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct))
        {
            logger.LogWarning("{Store}: ya hay una ejecución en curso; se omite esta", storeKey);
            return;
        }

        try
        {
            await RunLockedAsync(scraper, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RunLockedAsync(IStoreScraper scraper, CancellationToken ct)
    {
        var store = await EnsureStoreAsync(stores, unitOfWork, scraper.Store, ct);
        if (!store.IsActive)
        {
            logger.LogInformation("{Store}: tienda inactiva, no se ejecuta", store.Key);
            return;
        }

        var run = new ScrapeRun { StoreId = store.Id, StartedAt = clock.UtcNow, Status = ScrapeRunStatus.Running };
        runs.Add(run);
        await unitOfWork.SaveChangesAsync(ct);
        var runId = run.Id;
        var storeId = store.Id;
        unitOfWork.ClearTracking();

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("{Store}: iniciando recolección (ejecución {RunId})", scraper.Store.Key, runId);
        try
        {
            var stats = await ingestion.IngestAsync(storeId, scraper.ScrapeAsync(ct), ct);
            await FinishAsync(runId, ScrapeRunStatus.Succeeded, stats, null, CancellationToken.None);
            logger.LogInformation(
                "{Store}: {Found} productos, {Created} ofertas nuevas, {Changed} cambios de precio, {Unavailable} agotadas en {Elapsed}",
                scraper.Store.Key, stats.ItemsFound, stats.OffersCreated, stats.PricesChanged, stats.OffersMarkedUnavailable, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Store}: la recolección falló", scraper.Store.Key);
            unitOfWork.ClearTracking();
            await FinishAsync(runId, ScrapeRunStatus.Failed, null, ex.Message, CancellationToken.None);
            throw;
        }
    }

    private async Task FinishAsync(long runId, ScrapeRunStatus status, IngestionStats? stats, string? error, CancellationToken ct)
    {
        var run = await runs.GetAsync(runId, ct);
        if (run is null) return;
        run.Status = status;
        run.FinishedAt = clock.UtcNow;
        run.Error = error is { Length: > 4000 } ? error[..4000] : error;
        if (stats is not null)
        {
            run.ItemsFound = stats.ItemsFound;
            run.OffersCreated = stats.OffersCreated;
            run.PricesChanged = stats.PricesChanged;
            run.ItemsRejected = stats.ItemsRejected;
            run.OffersMarkedUnavailable = stats.OffersMarkedUnavailable;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Crea o actualiza la fila de la tienda con los datos del scraper.</summary>
    public static async Task<Store> EnsureStoreAsync(IStoreRepository stores, IUnitOfWork unitOfWork, StoreDescriptor descriptor, CancellationToken ct)
    {
        var store = await stores.GetByKeyAsync(descriptor.Key, ct);
        if (store is null)
        {
            store = new Store { Key = descriptor.Key, Name = descriptor.Name, BaseUrl = descriptor.BaseUrl, LogoUrl = descriptor.LogoUrl };
            stores.Add(store);
        }
        else
        {
            store.Name = descriptor.Name;
            store.BaseUrl = descriptor.BaseUrl;
            store.LogoUrl = descriptor.LogoUrl ?? store.LogoUrl;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return store;
    }
}
