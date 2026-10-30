using System.Runtime.CompilerServices;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Tiendas ficticias con datos generados, para desarrollo local y demos sin salir a
/// internet. Los precios varían según el día para que el historial tenga cambios.
/// Se activan con <c>Scrapers:Stores:demo-norte:Enabled=true</c> (y demo-sur).
/// </summary>
public sealed class DemoStoreScraper(StoreDescriptor store, int storeSeed, TimeProvider? timeProvider = null) : IStoreScraper
{
    public static readonly StoreDescriptor North = new("demo-norte", "Ferretería Demo Norte", "https://demo-norte.example");
    public static readonly StoreDescriptor South = new("demo-sur", "Ferretería Demo Sur", "https://demo-sur.example");

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public StoreDescriptor Store => store;

    private sealed record CatalogItem(string Title, string Brand, string Model, string? Ean, string Category, decimal BasePrice);

    // Títulos con distinta redacción en cada tienda para ejercitar el agrupamiento.
    private static readonly CatalogItem[] Catalog =
    [
        new("Taladro Percutor Inalámbrico 20V DCD778D2", "DeWalt", "DCD778D2", "7898935427473", "Taladros", 189990),
        new("Atornillador de Impacto 18V DTD157Z", "Makita", "DTD157Z", null, "Atornilladores", 129990),
        new("Esmeril Angular 4 1/2\" 850W GWS 850", "Bosch", "GWS850", "7891131029458", "Esmeriles", 54990),
        new("Sierra Circular 7 1/4\" 1400W CS1004", "Black+Decker", "CS1004", "7896070511415", "Sierras", 69990),
        new("Rotomartillo SDS Plus 800W HR2470", "Makita", "HR2470", "0088381077286", "Rotomartillos", 149990),
        new("Lijadora Orbital 1/4 Hoja 200W", "Bauker", "OS200-1", null, "Lijadoras", 24990),
        new("Set Llaves Combinadas 12 Piezas STMT74180", "Stanley", "STMT74180", null, "Herramientas manuales", 32990),
        new("Hidrolavadora K2 Compact 1400W", "Kärcher", "K2COMPACT", "4054278478135", "Hidrolavadoras", 99990),
        new("Compresor de Aire 50 Litros 2HP", "Einhell", "TC-AC 200/50", "4006825607178", "Compresores", 159990),
        new("Sierra Caladora 500W JS20", "Skil", "JS20", null, "Sierras", 39990),
        new("Multiherramienta 3000 con 25 accesorios", "Dremel", "F0133000JS", "8710364052547", "Multiherramientas", 64990),
        new("Martillo Carpintero 16oz Mango Fibra", "Truper", "MOF-16F", null, "Herramientas manuales", 12990),
    ];

    public async IAsyncEnumerable<ScrapedProduct> ScrapeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        for (int i = 0; i < Catalog.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            // Cada tienda tiene un surtido distinto: no todos los productos están en ambas.
            if ((i + storeSeed) % 5 == 0) continue;

            var item = Catalog[i];
            var price = PriceFor(item, i, today);
            var withEan = storeSeed % 2 == 0;
            yield return new ScrapedProduct
            {
                ExternalId = $"{store.Key}-{i + 1000}",
                // Una tienda informa marca/modelo/EAN; la otra solo los deja en el título.
                Title = withEan ? item.Title : $"{item.Brand} {item.Title} Mod. {item.Model}",
                Url = $"{store.BaseUrl}/p/{i + 1000}",
                Price = price,
                ListPrice = price < item.BasePrice ? item.BasePrice : null,
                IsAvailable = (i + today.DayNumber + storeSeed) % 11 != 0,
                Brand = withEan ? item.Brand : null,
                Model = withEan ? item.Model : null,
                Ean = withEan ? item.Ean : null,
                Category = item.Category,
            };
            await Task.Yield();
        }
    }

    // Precio estable durante el día, con variaciones de hasta ±15 % entre días y tiendas.
    private decimal PriceFor(CatalogItem item, int index, DateOnly day)
    {
        // Hash propio: HashCode.Combine cambia en cada ejecución del proceso.
        var hash = (index * 7919L + storeSeed * 104729L + day.DayNumber / 2 * 131L) % 31;
        var factor = 0.85m + hash / 100m;
        return Math.Round(item.BasePrice * factor / 10m) * 10m - 10m;
    }
}
