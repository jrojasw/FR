using AngleSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Platforms;
using PrecioHerramientas.Scrapers.Stores;
using PrecioHerramientas.Tests.Support;

namespace PrecioHerramientas.Tests;

public class ConstrumartScraperTests
{
    private const string Category = "https://www.construmart.cl/herramientas/herramientas-electricas-e-inalambricas/taladros-y-atornilladores";

    private static IOptions<ScraperOptions> OptionsWith(BranchOptions branches, params string[] queries) => Options.Create(new ScraperOptions
    {
        Stores = { [ConstrumartScraper.Key] = new StoreScraperOptions { DelayBetweenRequestsMs = 0, MaxPagesPerQuery = 3, Queries = queries.ToList(), Branches = branches } },
    });

    private static ConstrumartScraper Scraper(FakeBrowserSession session, IOptions<ScraperOptions> options) =>
        new(new ServiceCollection().AddSingleton<IBrowserSessionFactory>(session).BuildServiceProvider(), options, NullLogger<ConstrumartScraper>.Instance);

    private static async Task<List<ScrapedProduct>> CollectAsync(IAsyncEnumerable<ScrapedProduct> source)
    {
        var list = new List<ScrapedProduct>();
        await foreach (var item in source) list.Add(item);
        return list;
    }

    /// <summary>
    /// Sitio simulado: la búsqueda redirige a la categoría; la página 1 tiene la grilla y la 2
    /// viene vacía. El modal ofrece dos regiones y, según la elegida, sus tiendas.
    /// </summary>
    private static FakeBrowserSession Site()
    {
        var session = new FakeBrowserSession(url =>
            url.Contains("catalogsearch") ? new BrowserPage(Category, Fixture.Read("construmart-listing.html"), [])
            : url.Contains("p=2") ? new BrowserPage(url, "<html><body><div class='message notice'>No hay productos</div></body></html>", [])
            : new BrowserPage(url, "<html><body></body></html>", []));
        session.Options = (selector, chosen) => selector.Contains("region")
            ? [new("", "Selecciona tu región"), new("13", "Región Metropolitana de Santiago"), new("5", "Región de Valparaíso")]
            : chosen.Values.Contains("13")
                ? [new("", "Selecciona tu tienda"), new("CM-MAIPU", "Maipú"), new("CM-LAFLORIDA", "La Florida")]
                : [new("CM-VINA", "Viña del Mar")];
        return session;
    }

    [Fact]
    public async Task Lee_la_grilla_de_Magento_con_precio_de_data_price_amount()
    {
        using var document = await BrowsingContext.New(Configuration.Default)
            .OpenAsync(req => req.Content(Fixture.Read("construmart-listing.html")).Address(Category));

        var parsed = PrecioHerramientas.Scrapers.Parsing.HtmlListingParser.Parse(document, new Uri("https://www.construmart.cl"), ConstrumartScraper.Grid);

        Assert.Equal(3, parsed.Count);
        var taladro = parsed[0];
        Assert.Equal("1004521", taladro.ExternalId);                  // data-product-sku
        Assert.Equal("Taladro Percutor Eléctrico 650W GSB 13 RE Bosch", taladro.Title);
        Assert.Equal(49990m, taladro.Price);                          // "49990.0000", no 499.900.000
        Assert.Equal(59990m, taladro.ListPrice);
        Assert.Equal("https://www.construmart.cl/media/catalog/product/cache/1/gsb13re.jpg", taladro.ImageUrl);
        Assert.True(taladro.IsAvailable);

        var atornillador = parsed[1];
        Assert.Equal(89990m, atornillador.Price);
        Assert.False(atornillador.IsAvailable);                       // "Sin stock en esta tienda"
        Assert.Equal("https://www.construmart.cl/atornillador-inalambrico-12v-ddf083z-makita", atornillador.Url);

        var brocas = parsed[2];
        Assert.Equal(7990m, brocas.Price);                            // sin data-price-amount: el texto
        Assert.Equal("brocas-concreto-set-5-piezas", brocas.ExternalId);   // sin SKU: la ruta de la URL
    }

    [Fact]
    public async Task Recorre_cada_sucursal_de_la_region_y_corre_todas_las_busquedas_en_ella()
    {
        var session = Site();
        var scraper = Scraper(session, OptionsWith(new BranchOptions { Regions = ["Metropolitana"] }, "taladro", "atornillador"));

        var products = await CollectAsync(scraper.ScrapeAsync());

        // 3 productos × 2 sucursales (Maipú y La Florida); Viña del Mar no es de la región.
        Assert.Equal(6, products.Count);
        Assert.Equal(["CM-MAIPU", "CM-LAFLORIDA"], products.Select(p => p.BranchCode).Distinct());
        Assert.All(products.Where(p => p.BranchCode == "CM-MAIPU"), p => Assert.Equal("Maipú", p.BranchName));

        // Se elige cada sucursal una sola vez, y en ella se hacen ambas búsquedas.
        var storeChoices = session.Actions.Where(a => a.StartsWith("choose:") && a.Contains("CM-")).ToList();
        Assert.Equal(2, storeChoices.Count);
        Assert.Contains("=CM-MAIPU", storeChoices[0]);
        var searches = session.Loaded.Where(u => u.Contains("catalogsearch")).ToList();
        Assert.Equal(4, searches.Count);
        Assert.Contains("q=taladro", searches[0]);
        Assert.Contains("q=atornillador", searches[1]);

        // Paginación sobre la categoría a la que redirigió la búsqueda.
        Assert.Contains(session.Loaded, u => u == Category + "?p=2");
        Assert.Equal(1, session.Opened);      // una sola sesión de navegador
        Assert.Equal(1, session.Disposed);
    }

    [Fact]
    public async Task Con_lista_fija_y_cookie_conocida_no_usa_el_modal()
    {
        var session = Site();
        var branches = new BranchOptions
        {
            Regions = ["Metropolitana"],
            CookieName = "cm_store",
            List =
            [
                new BranchDefinition { Id = "CM-MAIPU", Name = "Maipú", Region = "Metropolitana" },
                new BranchDefinition { Id = "CM-VINA", Name = "Viña del Mar", Region = "Valparaíso" },
            ],
        };

        var products = await CollectAsync(Scraper(session, OptionsWith(branches, "taladro")).ScrapeAsync());

        Assert.Equal(3, products.Count);
        Assert.All(products, p => Assert.Equal("CM-MAIPU", p.BranchCode));
        Assert.Contains("cookie:cm_store=CM-MAIPU", session.Actions);
        Assert.DoesNotContain(session.Actions, a => a.StartsWith("choose:") || a.StartsWith("click:"));
    }

    [Fact]
    public async Task Falla_con_un_mensaje_claro_si_no_encuentra_sucursales()
    {
        var session = Site();
        session.Options = (_, _) => [];   // el modal no aparece o cambió

        var error = await Assert.ThrowsAsync<ScrapeFailedException>(() =>
            CollectAsync(Scraper(session, OptionsWith(new BranchOptions { Regions = ["Metropolitana"] }, "taladro")).ScrapeAsync()));

        Assert.Contains("no se encontraron sucursales", error.Message);
        Assert.Contains("Branches:Selectors", error.Message);
    }

    [Fact]
    public async Task Si_una_sucursal_no_se_puede_elegir_sigue_con_las_demas()
    {
        // "Sucursal cerrada" está en la lista fija, pero el selector del sitio no la ofrece.
        var session = Site();
        var branches = new BranchOptions
        {
            Regions = ["Metropolitana"],
            List =
            [
                new BranchDefinition { Id = "CM-MAIPU", Name = "Maipú", Region = "Metropolitana" },
                new BranchDefinition { Id = "CM-CERRADA", Name = "Sucursal cerrada", Region = "Metropolitana" },
            ],
        };

        var products = await CollectAsync(Scraper(session, OptionsWith(branches, "taladro")).ScrapeAsync());

        Assert.Equal(3, products.Count);
        Assert.All(products, p => Assert.Equal("CM-MAIPU", p.BranchCode));
    }
}
