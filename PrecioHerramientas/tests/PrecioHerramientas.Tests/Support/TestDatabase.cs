using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Services;
using PrecioHerramientas.Infrastructure.Persistence;
using PrecioHerramientas.Infrastructure.Repositories;

namespace PrecioHerramientas.Tests.Support;

public sealed class ManualClock(DateTime start) : IClock
{
    public DateTime UtcNow { get; set; } = start;
}

/// <summary>Base en memoria + servicio de ingesta con los repositorios reales.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _name = Guid.NewGuid().ToString();

    public ManualClock Clock { get; } = new(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    public AppDbContext Db { get; }

    public TestDatabase()
    {
        Db = NewContext();
    }

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_name).Options);

    public async Task<Store> AddStoreAsync(string key)
    {
        var store = new Store { Key = key, Name = key.ToUpperInvariant(), BaseUrl = $"https://{key}.example" };
        Db.Stores.Add(store);
        await Db.SaveChangesAsync();
        return store;
    }

    public async Task<IngestionStats> IngestAsync(Store store, params ScrapedProduct[] items)
    {
        // Contexto nuevo por ejecución, como ocurre con cada trabajo de Hangfire.
        using var db = NewContext();
        var service = new PriceIngestionService(
            new ProductRepository(db), new OfferRepository(db), new UnitOfWork(db), Clock,
            Options.Create(new IngestionOptions { BatchSize = 2, MinCoverageToMarkUnavailable = 0.5 }),
            NullLogger<PriceIngestionService>.Instance);
        return await service.IngestAsync(store.Id, ToAsync(items));
    }

    private static async IAsyncEnumerable<ScrapedProduct> ToAsync(IEnumerable<ScrapedProduct> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    public void Dispose() => Db.Dispose();
}
