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
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(handler))
            .AddSingleton<HttpPageFetcher>()
            .BuildServiceProvider();
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

    [Fact]
    public void Sodimac_lee_next_data_sin_precio_cmr()
    {
        var scraper = new SodimacScraper(new ServiceCollection().BuildServiceProvider(), OptionsFor(SodimacScraper.Key), NullLogger<SodimacScraper>.Instance);

        var products = scraper.ParseNextData(Fixture.Read("sodimac-next-data.json"));

        var p = Assert.Single(products);
        Assert.Equal("110001X", p.ExternalId);
        Assert.Equal(149990m, p.Price);      // precio internet, no el de tarjeta CMR
        Assert.Equal(169990m, p.ListPrice);
        Assert.Equal("MAKITA", p.Brand);
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
