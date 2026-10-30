using AngleSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Stores;
using PrecioHerramientas.Tests.Support;

namespace PrecioHerramientas.Tests;

public class ScraperParsingTests
{
    private static IOptions<ScraperOptions> OptionsFor(string key, params string[] queries) => Options.Create(new ScraperOptions
    {
        Stores = { [key] = new StoreScraperOptions { Enabled = true, DelayBetweenRequestsMs = 0, MaxPagesPerQuery = 5, Queries = queries.ToList() } },
    });

    private static async Task<List<ScrapedProduct>> CollectAsync(IAsyncEnumerable<ScrapedProduct> source)
    {
        var list = new List<ScrapedProduct>();
        await foreach (var item in source) list.Add(item);
        return list;
    }

    [Fact]
    public async Task Vtex_mapea_precios_stock_y_variantes_y_pagina()
    {
        var handler = new FakeHttpHandler(req => FakeHttpHandler.Ok(
            req.RequestUri!.Query.Contains("_from=0&") ? Fixture.Read("vtex-search.json") : "[]"));
        var scraper = new EasyScraper(new FakeHttpClientFactory(handler), OptionsFor(EasyScraper.Key, "taladro"), NullLogger<EasyScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        // El ítem con precio 0 se descarta; la página trae menos de 50 productos, así que no pide la siguiente.
        Assert.Equal(2, products.Count);
        Assert.Single(handler.Requests);
        Assert.Contains("ft=taladro", handler.Requests[0].Query);

        var taladro = products[0];
        Assert.Equal("55001", taladro.ExternalId);
        Assert.Equal(179990m, taladro.Price);
        Assert.Equal(219990m, taladro.ListPrice);
        Assert.True(taladro.IsAvailable);
        Assert.Equal("DEWALT", taladro.Brand);
        Assert.Equal("DCD778D2-B2", taladro.Model);
        Assert.Equal("7898935427473", taladro.Ean);
        Assert.Equal("Herramientas/Herramientas Eléctricas/Taladros", taladro.Category);

        var puntas = products[1];
        Assert.Equal("Juego de puntas 32 piezas", puntas.Title);   // con variantes se usa el nombre del SKU
        Assert.Equal("https://www.easy.cl/juego-de-puntas/p", puntas.Url);
        Assert.False(puntas.IsAvailable);
    }

    [Fact]
    public async Task HtmlListing_lee_tarjetas_con_selectores()
    {
        var handler = new FakeHttpHandler(req => req.RequestUri!.Query.Contains("p=1")
            ? FakeHttpHandler.Ok(Fixture.Read("listing.html"), "text/html")
            : FakeHttpHandler.Ok("<html><body></body></html>", "text/html"));
        var services = Fetchers(new HttpPageFetcher(new FakeHttpClientFactory(handler), NullLogger<HttpPageFetcher>.Instance));
        var scraper = new ImperialScraper(services, OptionsFor(ImperialScraper.Key, "taladro"), NullLogger<ImperialScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal(2, products.Count);
        Assert.Equal("IMP-1", products[0].ExternalId);
        Assert.Equal("https://www.imperial.cl/taladro-bosch-gsb-13-re.html", products[0].Url);
        Assert.Equal(49990m, products[0].Price);
        Assert.Equal(59990m, products[0].ListPrice);
        Assert.Equal("Bosch", products[0].Brand);
        Assert.Equal("https://www.imperial.cl/media/gsb13.jpg", products[0].ImageUrl);
        Assert.True(products[0].IsAvailable);

        Assert.Equal("https://www.imperial.cl/esmeril.html", products[1].Url);   // sin parámetros de tracking
        Assert.Equal(1234567m, products[1].Price);
        Assert.Equal("https://cdn.example/ga4530.jpg", products[1].ImageUrl);
        Assert.False(products[1].IsAvailable);
    }

    [Fact]
    public async Task JsonLd_lee_ItemList_de_productos()
    {
        var document = await BrowsingContext.New(Configuration.Default)
            .OpenAsync(req => req.Content(Fixture.Read("jsonld.html")).Address("https://tienda.example/buscar"));

        var products = JsonLdProductExtractor.Extract(document, new Uri("https://tienda.example"));

        Assert.Equal(2, products.Count);
        var sierra = products[0];
        Assert.Equal("S-77", sierra.ExternalId);
        Assert.Equal("https://tienda.example/p/sierra-cs1004", sierra.Url);
        Assert.Equal(69990m, sierra.Price);
        Assert.Equal("BLACK+DECKER", sierra.Brand);
        Assert.Equal("CS1004", sierra.Model);
        Assert.Equal("7896070511415", sierra.Ean);
        Assert.Equal("https://tienda.example/img/cs1004.jpg", sierra.ImageUrl);
        Assert.True(sierra.IsAvailable);
        Assert.False(products[1].IsAvailable);
    }

    private static IServiceProvider Fetchers(IPageFetcher http, IPageFetcher? browser = null) => new ServiceCollection()
        .AddKeyedSingleton(PageFetcherKeys.Http, http)
        .AddKeyedSingleton(PageFetcherKeys.Browser, browser ?? new FakePageFetcher(_ => throw new InvalidOperationException("No debía usarse el navegador")))
        .BuildServiceProvider();

    private static SodimacScraper Sodimac(IServiceProvider services) =>
        new(services, OptionsFor(SodimacScraper.Key, "taladro"), NullLogger<SodimacScraper>.Instance);

    [Fact]
    public void Sodimac_lee_next_data_con_precio_internet_tachado_y_rangos()
    {
        var products = Sodimac(Fetchers(new FakePageFetcher(_ => null))).ParseNextData(Fixture.Read("sodimac-next-data.json"));

        Assert.Equal(3, products.Count);   // el producto sin precio se descarta

        var roto = products[0];
        Assert.Equal("110001X", roto.ExternalId);
        Assert.Equal(149990m, roto.Price);      // precio internet, no el de tarjeta CMR
        Assert.Equal(169990m, roto.ListPrice);  // precio normal tachado
        Assert.Equal("MAKITA", roto.Brand);
        Assert.Equal("HR2470", roto.Model);     // desde topSpecifications
        Assert.Equal("https://media.falabella.com/sodimacCL/110001/w=200", roto.ImageUrl);

        var brocas = products[1];
        Assert.Equal(7990m, brocas.Price);      // rango de precios por variantes: el menor
        Assert.Equal(12990m, brocas.ListPrice);
        Assert.Equal("https://www.sodimac.cl/sodimac-cl/product/220002/set-brocas/220002", brocas.Url);
        Assert.Null(brocas.ImageUrl);

        var taladro = products[2];
        Assert.Equal("330003", taladro.ExternalId);   // sin skuId se usa productId
        Assert.Equal(59990m, taladro.Price);          // solo precio normal: ese es el que se paga
        Assert.Null(taladro.ListPrice);
    }

    [Fact]
    public void Sodimac_encuentra_results_aunque_cambie_de_lugar()
    {
        var json = """{"props":{"pageProps":{"searchData":{"results":[{"skuId":"1","displayName":"Taladro","url":"/p/1","prices":[{"type":"internetPrice","price":["10.990"]}]}]}}}}""";

        var product = Assert.Single(Sodimac(Fetchers(new FakePageFetcher(_ => null))).ParseNextData(json));

        Assert.Equal(10990m, product.Price);
    }

    [Fact]
    public async Task Sodimac_busca_por_HTTP_en_la_url_real_y_pagina()
    {
        var requested = new List<string>();
        var http = new FakePageFetcher(url =>
        {
            requested.Add(url);
            return url.Contains("page=1") ? Fixture.Read("sodimac-search.html") : "<html><script id=\"__NEXT_DATA__\">{\"props\":{\"pageProps\":{\"results\":[]}}}</script></html>";
        });

        var products = await CollectAsync(Sodimac(Fetchers(http)).ScrapeAsync());

        Assert.Equal(3, products.Count);
        Assert.Equal("https://www.sodimac.cl/sodimac-cl/buscar?Ntt=taladro&page=1", requested[0]);
        Assert.Equal(2, requested.Count);   // la página 2 vino vacía y se detuvo
    }

    [Fact]
    public async Task Sodimac_usa_navegador_si_HTTP_es_bloqueado()
    {
        var http = new FakePageFetcher(_ => throw new HttpRequestException("Forbidden", null, System.Net.HttpStatusCode.Forbidden));
        var browserCalls = 0;
        var browser = new FakePageFetcher(url => { browserCalls++; return url.Contains("page=1") ? Fixture.Read("sodimac-search.html") : null; });

        var products = await CollectAsync(Sodimac(Fetchers(http, browser)).ScrapeAsync());

        Assert.Equal(3, products.Count);
        Assert.Equal(2, browserCalls);   // después del bloqueo, el resto de la ejecución va por navegador
    }

    [Fact]
    public async Task Sodimac_usa_navegador_si_HTTP_trae_una_pagina_sin_datos()
    {
        var http = new FakePageFetcher(_ => "<html><body>Verificando que eres humano…</body></html>");
        var browser = new FakePageFetcher(url => url.Contains("page=1") ? Fixture.Read("sodimac-search.html") : null);

        var products = await CollectAsync(Sodimac(Fetchers(http, browser)).ScrapeAsync());

        Assert.Equal(3, products.Count);
    }

    [Fact]
    public async Task SearchUrlTemplate_de_configuracion_reemplaza_la_url()
    {
        var requested = new List<string>();
        var options = Options.Create(new ScraperOptions
        {
            Stores = { [SodimacScraper.Key] = new StoreScraperOptions { DelayBetweenRequestsMs = 0, MaxPagesPerQuery = 1, Queries = ["sierra circular"], SearchUrlTemplate = "/nueva/busqueda?q={query}&p={page}" } },
        });
        var scraper = new SodimacScraper(Fetchers(new FakePageFetcher(url => { requested.Add(url); return null; })), options, NullLogger<SodimacScraper>.Instance);

        await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal("https://www.sodimac.cl/nueva/busqueda?q=sierra%20circular&p=1", Assert.Single(requested));
    }

    [Fact]
    public void Selectores_de_configuracion_sobrescriben_los_por_defecto()
    {
        var defaults = new ListingSelectors { Card = ".a", Title = ".t", Link = "a", Price = ".p", Brand = ".b" };

        var merged = defaults.WithOverrides(new Dictionary<string, string> { ["Card"] = ".nueva", ["Brand"] = "" });

        Assert.Equal(".nueva", merged.Card);
        Assert.Null(merged.Brand);
        Assert.Equal(".t", merged.Title);
    }
}
