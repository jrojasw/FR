using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;
using PrecioHerramientas.Scrapers.Stores;
using PrecioHerramientas.Tests.Support;

namespace PrecioHerramientas.Tests;

public class BrowserCaptureScraperTests
{
    private static IOptions<ScraperOptions> OptionsFor(string key, params string[] queries) => Options.Create(new ScraperOptions
    {
        Stores = { [key] = new StoreScraperOptions { Enabled = true, DelayBetweenRequestsMs = 0, MaxPagesPerQuery = 5, Queries = queries.ToList() } },
    });

    private static IServiceProvider With(FakeBrowserSession session) =>
        new ServiceCollection().AddSingleton<IBrowserSessionFactory>(session).BuildServiceProvider();

    private static async Task<List<ScrapedProduct>> CollectAsync(IAsyncEnumerable<ScrapedProduct> source)
    {
        var list = new List<ScrapedProduct>();
        await foreach (var item in source) list.Add(item);
        return list;
    }

    private static BrowserPage Page(string finalUrl, string html = "<html><body></body></html>", params CapturedJson[] responses) =>
        new(finalUrl, html, responses);

    // ---------- Parser de respuestas capturadas ----------

    [Fact]
    public void Parser_reconoce_Intelligent_Search_de_VTEX()
    {
        var products = CapturedJsonProductParser.Parse(Fixture.Read("easy-intelligent-search.json"), new Uri("https://www.easy.cl"));

        Assert.Equal(2, products.Count);
        var taladro = products[0];
        Assert.Equal("1180001", taladro.ExternalId);
        Assert.Equal("https://www.easy.cl/taladro-percutor-inalambrico-20v-dcd778d2/p", taladro.Url);
        Assert.Equal(169990m, taladro.Price);
        Assert.Equal(199990m, taladro.ListPrice);
        Assert.Equal("DeWalt", taladro.Brand);
        Assert.Equal("DCD778D2", taladro.Model);
        Assert.Equal("7898935427473", taladro.Ean);
        Assert.False(products[1].IsAvailable);
    }

    [Fact]
    public void Parser_reconoce_GraphQL_productSearch_de_VTEX_IO()
    {
        var product = Assert.Single(CapturedJsonProductParser.Parse(Fixture.Read("easy-graphql-page2.json"), new Uri("https://www.easy.cl")));

        Assert.Equal("1180003", product.ExternalId);
        Assert.Equal(52990m, product.Price);
        Assert.True(product.IsAvailable);   // sin IsAvailable, se usa el stock
        Assert.Equal("https://www.easy.cl/esmeril-angular-850w-gws-850/p", product.Url);
    }

    [Fact]
    public void Parser_reconoce_busqueda_guiada_de_Oracle_Commerce()
    {
        var products = CapturedJsonProductParser.Parse(Fixture.Read("imperial-occ-search.json"), new Uri("https://www.imperial.cl"));

        Assert.Equal(2, products.Count);
        var gsb = products[0];
        Assert.Equal("IMP-1001-01", gsb.ExternalId);
        Assert.Equal("Taladro Percutor 650W GSB 13 RE", gsb.Title);
        Assert.Equal("https://www.imperial.cl/taladro-percutor-650w-gsb-13-re/product/IMP-1001", gsb.Url);
        Assert.Equal(49990m, gsb.Price);
        Assert.Equal(64990m, gsb.ListPrice);
        Assert.Equal("Bosch", gsb.Brand);
        Assert.Equal("GSB 13 RE", gsb.Model);
        Assert.Equal("7891131029458", gsb.Ean);
        Assert.StartsWith("https://www.imperial.cl/ccstore/v1/images/", gsb.ImageUrl);

        var ddf = products[1];
        Assert.Equal("IMP-1002", ddf.ExternalId);   // sin SKU se usa el id del producto
        Assert.Equal(89990m, ddf.Price);            // sin precio de oferta, el normal
        Assert.Null(ddf.ListPrice);
        Assert.False(ddf.IsAvailable);
    }

    [Fact]
    public void Parser_reconoce_listProducts_de_Oracle_Commerce_con_listas_de_precios()
    {
        var product = Assert.Single(CapturedJsonProductParser.Parse(Fixture.Read("imperial-occ-products.json"), new Uri("https://www.imperial.cl")));

        Assert.Equal("IMP-1003", product.ExternalId);
        Assert.Equal(119990m, product.Price);
        Assert.Equal(129990m, product.ListPrice);
        Assert.Equal("Redline", product.Brand);
        Assert.Equal("https://www.imperial.cl/file/v123/products/taladro-banco.jpg", product.ImageUrl);
    }

    [Theory]
    [InlineData("""{"cart":{"items":[]},"user":{"name":"x"}}""")]
    [InlineData("""[{"event":"pageview"}]""")]
    [InlineData("esto no es json")]
    public void Parser_ignora_respuestas_que_no_son_listados(string json) =>
        Assert.Empty(CapturedJsonProductParser.Parse(json, new Uri("https://www.easy.cl")));

    // ---------- Paginación de la API capturada ----------

    [Theory]
    [InlineData("https://x.cl/api/catalog_system/pub/products/search?ft=taladro&_from=0&_to=49", 3, "_from=100&_to=149")]
    [InlineData("https://x.cl/api/io/_v/api/intelligent-search/product_search/?query=taladro&page=1&count=24", 2, "page=2")]
    [InlineData("https://x.cl/api/search?q=taladro&from=0&to=23", 2, "from=24&to=47")]
    [InlineData("https://x.cl/ccstoreui/v1/search?Ntt=taladro&No=0&Nrpp=12", 3, "No=24&Nrpp=12")]
    [InlineData("https://x.cl/ccstore/v1/products?categoryId=000300030352&offset=0&limit=24", 2, "offset=24&limit=24")]
    [InlineData("https://x.cl/ccstoreui/v1/search?N=123&Nrpp=12", 2, "N=123&Nrpp=12&No=12")]
    public void PagingUrl_cambia_solo_la_paginacion(string url, int page, string expected) =>
        Assert.Contains(expected, PagingUrl.ForPage(url, page));

    [Fact]
    public void PagingUrl_ajusta_variables_en_base64_de_GraphQL_de_VTEX_IO()
    {
        var variables = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("""{"query":"taladro","from":0,"to":11}"""));
        var url = "https://www.easy.cl/_v/segment/graphql/v1?operationName=productSearchV3&extensions="
            + Uri.EscapeDataString($$"""{"persistedQuery":{"version":1},"variables":"{{variables}}"}""");

        var next = PagingUrl.ForPage(url, 3)!;

        var extensions = System.Text.Json.Nodes.JsonNode.Parse(System.Web.HttpUtility.ParseQueryString(new Uri(next).Query)["extensions"]!)!;
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(extensions["variables"]!.GetValue<string>()));
        Assert.Contains("\"from\":24", decoded);
        Assert.Contains("\"to\":35", decoded);
        Assert.Contains("\"query\":\"taladro\"", decoded);
    }

    [Fact]
    public void PagingUrl_devuelve_null_si_no_reconoce_paginacion() =>
        Assert.Null(PagingUrl.ForPage("https://x.cl/api/minicart?id=1", 2));

    // ---------- Easy ----------

    [Fact]
    public async Task Easy_abre_la_busqueda_publica_y_pagina_por_la_API_que_uso_el_sitio()
    {
        var api = "https://www.easy.cl/api/io/_v/api/intelligent-search/product_search/?query=esmeril%20angular&page=1&count=2";
        var session = new FakeBrowserSession(
            load: url => Page(url, responses:
            [
                new CapturedJson("https://www.easy.cl/api/checkout/pub/orderForm", """{"items":[]}"""),
                new CapturedJson(api, Fixture.Read("easy-intelligent-search.json")),
            ]),
            get: url => url.Contains("page=2") ? Fixture.Read("easy-graphql-page2.json") : """{"products":[]}""");
        var scraper = new EasyScraper(With(session), OptionsFor(EasyScraper.Key, "esmeril angular"), NullLogger<EasyScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal("https://www.easy.cl/search/esmeril%20angular", Assert.Single(session.Loaded));
        Assert.Equal(3, products.Count);
        Assert.Contains("page=2", session.Fetched[0]);
        Assert.Contains("page=3", session.Fetched[1]);   // la página 3 vino vacía y se detuvo
        Assert.Equal(2, session.Fetched.Count);
        Assert.Equal(1, session.Opened);
        Assert.Equal(1, session.Disposed);               // el navegador se cierra al terminar
    }

    [Fact]
    public async Task Easy_lee_el_HTML_renderizado_si_no_reconoce_ninguna_API()
    {
        var session = new FakeBrowserSession(url => url.Contains("page=") ? null : Page(url, Fixture.Read("jsonld.html")));
        var scraper = new EasyScraper(With(session), OptionsFor(EasyScraper.Key, "sierra circular"), NullLogger<EasyScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal(2, products.Count);
        Assert.Equal("https://www.easy.cl/p/sierra-cs1004", products[0].Url);
        Assert.Equal("https://www.easy.cl/search/sierra%20circular?page=2", session.Loaded[1]);   // paginación por URL
    }

    [Fact]
    public async Task Easy_falla_la_ejecucion_si_no_aparece_ningun_producto()
    {
        var session = new FakeBrowserSession(url => Page(url, responses: new CapturedJson("https://www.easy.cl/api/x", """{"ok":true}""")));
        var scraper = new EasyScraper(With(session), OptionsFor(EasyScraper.Key, "taladro"), NullLogger<EasyScraper>.Instance);

        var error = await Assert.ThrowsAsync<ScrapeFailedException>(() => CollectAsync(scraper.ScrapeAsync()));

        Assert.Contains("1 respuestas JSON revisadas", error.Message);
        Assert.Equal(1, session.Disposed);
    }

    [Fact]
    public async Task Si_el_navegador_no_inicia_la_ejecucion_falla_con_el_motivo()
    {
        var session = new FakeBrowserSession(_ => throw new InvalidOperationException("No se pudo iniciar Chromium para Playwright"));
        var scraper = new EasyScraper(With(session), OptionsFor(EasyScraper.Key, "taladro", "lijadora"), NullLogger<EasyScraper>.Instance);

        var error = await Assert.ThrowsAsync<ScrapeFailedException>(() => CollectAsync(scraper.ScrapeAsync()));

        Assert.Contains("No se pudo iniciar Chromium", error.Message);
    }

    // ---------- Imperial ----------

    [Fact]
    public async Task Imperial_sigue_la_redireccion_a_la_categoria_y_pagina_por_la_API_de_Oracle_Commerce()
    {
        var category = "https://www.imperial.cl/taladros/category/000300030352";
        var api = "https://www.imperial.cl/ccstoreui/v1/search?N=3052&Nrpp=2&No=0";
        var session = new FakeBrowserSession(
            load: url => Page(category, responses: new CapturedJson(api, Fixture.Read("imperial-occ-search.json"))),
            get: url => url.Contains("No=2") ? Fixture.Read("imperial-occ-products.json") : """{"resultsList":{"records":[]}}""");
        var scraper = new ImperialScraper(With(session), OptionsFor(ImperialScraper.Key, "taladro"), NullLogger<ImperialScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal("https://www.imperial.cl/searchresults?Ntt=taladro", Assert.Single(session.Loaded));
        Assert.Equal(3, products.Count);
        Assert.Equal(["IMP-1001-01", "IMP-1002", "IMP-1003"], products.Select(p => p.ExternalId));
        Assert.Contains("No=2", session.Fetched[0]);
        Assert.Contains("No=4", session.Fetched[1]);
    }

    [Fact]
    public async Task Imperial_acepta_rutas_de_categoria_como_busqueda()
    {
        var session = new FakeBrowserSession(url => Page(url, responses: new CapturedJson("https://www.imperial.cl/ccstore/v1/products?categoryId=000300030352&offset=0&limit=2", Fixture.Read("imperial-occ-products.json"))));
        var scraper = new ImperialScraper(With(session), OptionsFor(ImperialScraper.Key, "/taladros/category/000300030352"), NullLogger<ImperialScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal("https://www.imperial.cl/taladros/category/000300030352", session.Loaded[0]);
        Assert.Single(products);
    }

    [Fact]
    public async Task Imperial_lee_la_grilla_renderizada_con_precio_final_prioritario()
    {
        var session = new FakeBrowserSession(url => Page("https://www.imperial.cl/taladros/category/000300030352", Fixture.Read("imperial-listing.html")));
        var scraper = new ImperialScraper(With(session), OptionsFor(ImperialScraper.Key, "taladro"), NullLogger<ImperialScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal(2, products.Count);
        Assert.Equal(49990m, products[0].Price);
        Assert.Equal(64990m, products[0].ListPrice);
        Assert.Equal("https://www.imperial.cl/taladro-percutor-bosch-gsb-13-re/p/500", products[0].Url);
        Assert.False(products[1].IsAvailable);
    }

    [Fact]
    public async Task Imperial_permite_otra_url_de_busqueda_por_configuracion()
    {
        var session = new FakeBrowserSession(url => Page(url, Fixture.Read("imperial-listing.html")));
        var options = Options.Create(new ScraperOptions
        {
            Stores = { [ImperialScraper.Key] = new StoreScraperOptions { DelayBetweenRequestsMs = 0, MaxPagesPerQuery = 1, Queries = ["taladro"], SearchUrlTemplate = "/search?Ntt={query}" } },
        });

        await CollectAsync(new ImperialScraper(With(session), options, NullLogger<ImperialScraper>.Instance).ScrapeAsync());

        Assert.Equal("https://www.imperial.cl/search?Ntt=taladro", session.Loaded[0]);
    }
}
