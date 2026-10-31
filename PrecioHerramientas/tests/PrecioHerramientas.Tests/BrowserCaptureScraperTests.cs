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

    /// <summary>Servicios con HTTP simulado (opcional) y sesión de navegador simulada.</summary>
    private static IServiceProvider With(FakeBrowserSession session, Func<HttpRequestMessage, HttpResponseMessage>? http) =>
        http is null
            ? With(session)
            : new ServiceCollection()
                .AddSingleton<IBrowserSessionFactory>(session)
                .AddSingleton<IHttpClientFactory>(new FakeHttpClientFactory(new FakeHttpHandler(http)))
                .BuildServiceProvider();

    private static HttpResponseMessage Html(string html) => FakeHttpHandler.Ok(html, "text/html");
    private static HttpResponseMessage Status(System.Net.HttpStatusCode code) => new(code);

    // ---------- Easy: JSON de Next.js ----------

    [Fact]
    public void EasyNextData_lee_productos_total_y_buildId_del_HTML()
    {
        var json = EasyNextDataParser.ExtractNextDataJson(Fixture.Read("easy-search-page.html"))!;

        var result = EasyNextDataParser.Parse(json, new Uri("https://www.easy.cl"));

        Assert.Equal("Xk2v9_build-2026-10-30", result.BuildId);
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Products.Count);

        var taladro = result.Products[0];
        Assert.Equal("1180001", taladro.ExternalId);
        Assert.Equal("Taladro percutor inalámbrico 20V DCD778D2", taladro.Title);
        Assert.Equal("https://www.easy.cl/taladro-percutor-inalambrico-20v-dcd778d2/p", taladro.Url);
        Assert.Equal(169990m, taladro.Price);        // offerPrice
        Assert.Equal(199990m, taladro.ListPrice);    // normalPrice
        Assert.Equal("DeWalt", taladro.Brand);
        Assert.True(taladro.IsAvailable);
        Assert.Equal("https://easycl.vtexassets.com/arquivos/ids/1180001/imagen.jpg", taladro.ImageUrl);

        var roto = result.Products[1];
        Assert.Equal(154990m, roto.Price);           // precios como texto "$154.990"
        Assert.Null(roto.ListPrice);                 // normal = oferta: no hay descuento
        Assert.False(roto.IsAvailable);              // availableQuantity 0
        Assert.Null(roto.ImageUrl);
    }

    [Fact]
    public void EasyNextData_lee_la_respuesta_de_next_data()
    {
        var result = EasyNextDataParser.Parse(Fixture.Read("easy-next-data-page2.json"), new Uri("https://www.easy.cl"));

        Assert.Null(result.BuildId);
        Assert.Equal("1180003", Assert.Single(result.Products).ExternalId);
    }

    [Fact]
    public async Task Easy_usa_next_data_por_HTTP_con_el_buildId_de_la_pagina()
    {
        var requested = new List<string>();
        var session = new FakeBrowserSession(_ => throw new InvalidOperationException("No debía usarse el navegador"));
        var services = With(session, req =>
        {
            var url = req.RequestUri!.PathAndQuery;
            requested.Add(url);
            return url switch
            {
                "/search/taladro" => Html(Fixture.Read("easy-search-page.html")),
                "/_next/data/Xk2v9_build-2026-10-30/search/taladro.json?search=taladro&page=2" => FakeHttpHandler.Ok(Fixture.Read("easy-next-data-page2.json")),
                _ => Status(System.Net.HttpStatusCode.NotFound),
            };
        });
        var scraper = new EasyScraper(services, OptionsFor(EasyScraper.Key, "taladro"), NullLogger<EasyScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal(["1180001", "1180002", "1180003"], products.Select(p => p.ExternalId));
        // Con recordsFiltered = 3 ya se tienen todos: no pide la página 3.
        Assert.Equal(["/search/taladro", "/_next/data/Xk2v9_build-2026-10-30/search/taladro.json?search=taladro&page=2"], requested);
        Assert.Equal(0, session.Opened);
    }

    [Fact]
    public async Task Easy_relee_el_buildId_si_Easy_publico_una_version_nueva()
    {
        var htmlRequests = 0;
        var services = With(new FakeBrowserSession(_ => null), req =>
        {
            var url = req.RequestUri!.PathAndQuery;
            if (url == "/search/taladro")
            {
                // La segunda vez la página ya trae el buildId nuevo.
                var html = Fixture.Read("easy-search-page.html");
                return Html(htmlRequests++ == 0 ? html : html.Replace("Xk2v9_build-2026-10-30", "Nuevo_build"));
            }
            return url.StartsWith("/_next/data/Nuevo_build/") ? FakeHttpHandler.Ok(Fixture.Read("easy-next-data-page2.json")) : Status(System.Net.HttpStatusCode.NotFound);
        });

        var products = await CollectAsync(new EasyScraper(services, OptionsFor(EasyScraper.Key, "taladro"), NullLogger<EasyScraper>.Instance).ScrapeAsync());

        Assert.Equal(3, products.Count);
        Assert.Equal(2, htmlRequests);
    }

    [Fact]
    public async Task Easy_pasa_a_Chromium_si_HTTP_es_bloqueado()
    {
        var session = new FakeBrowserSession(url => url.Contains("/search/") ? Page(url, Fixture.Read("easy-search-page.html")) : Page(url));
        var services = With(session, _ => Status(System.Net.HttpStatusCode.Forbidden));

        var products = await CollectAsync(new EasyScraper(services, OptionsFor(EasyScraper.Key, "taladro"), NullLogger<EasyScraper>.Instance).ScrapeAsync());

        Assert.Equal(2, products.Count);
        Assert.Equal("https://www.easy.cl/", session.Loaded[0]);   // primero la portada, por las cookies
        Assert.Equal("https://www.easy.cl/search/taladro", session.Loaded[1]);
        Assert.Contains("/_next/data/Xk2v9_build-2026-10-30/", session.Fetched[0]);   // la página 2 también dentro de la sesión
    }

    // ---------- Easy: respaldo leyendo lo que pide la página ----------

    private static IServiceProvider PageWithoutNextData(FakeBrowserSession session) =>
        With(session, _ => Html("<html><body>Cargando…</body></html>"));

    [Fact]
    public async Task Easy_sin_next_data_lee_la_API_que_uso_el_sitio_y_pagina()
    {
        var api = "https://www.easy.cl/api/io/_v/api/intelligent-search/product_search/?query=esmeril%20angular&page=1&count=2";
        var session = new FakeBrowserSession(
            load: url => Page(url, responses:
            [
                new CapturedJson("https://www.easy.cl/api/checkout/pub/orderForm", """{"items":[]}"""),
                new CapturedJson(api, Fixture.Read("easy-intelligent-search.json")),
            ]),
            get: url => url.Contains("page=2") ? Fixture.Read("easy-graphql-page2.json") : """{"products":[]}""");
        var scraper = new EasyScraper(PageWithoutNextData(session), OptionsFor(EasyScraper.Key, "esmeril angular"), NullLogger<EasyScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal("https://www.easy.cl/search/esmeril%20angular", Assert.Single(session.Loaded));
        Assert.Equal(3, products.Count);
        Assert.Contains("page=2", session.Fetched[0]);
        Assert.Contains("page=3", session.Fetched[1]);   // la página 3 vino vacía y se detuvo
        Assert.Equal(1, session.Disposed);               // el navegador se cierra al terminar
    }

    [Fact]
    public async Task Easy_sin_next_data_lee_el_HTML_renderizado()
    {
        var session = new FakeBrowserSession(url => url.Contains("page=") ? null : Page(url, Fixture.Read("jsonld.html")));
        var scraper = new EasyScraper(PageWithoutNextData(session), OptionsFor(EasyScraper.Key, "sierra circular"), NullLogger<EasyScraper>.Instance);

        var products = await CollectAsync(scraper.ScrapeAsync());

        Assert.Equal(2, products.Count);
        Assert.Equal("https://www.easy.cl/p/sierra-cs1004", products[0].Url);
    }

    [Fact]
    public async Task Easy_falla_la_ejecucion_si_no_aparece_ningun_producto()
    {
        var session = new FakeBrowserSession(url => Page(url, responses: new CapturedJson("https://www.easy.cl/api/x", """{"ok":true}""")));
        var scraper = new EasyScraper(PageWithoutNextData(session), OptionsFor(EasyScraper.Key, "taladro"), NullLogger<EasyScraper>.Instance);

        var error = await Assert.ThrowsAsync<ScrapeFailedException>(() => CollectAsync(scraper.ScrapeAsync()));

        Assert.Contains("1 respuestas JSON revisadas", error.Message);
        Assert.Equal(1, session.Disposed);
    }

    [Fact]
    public async Task Si_el_navegador_no_inicia_la_ejecucion_falla_con_el_motivo()
    {
        var session = new FakeBrowserSession(_ => throw new InvalidOperationException("No se pudo iniciar Chromium para Playwright"));
        var scraper = new EasyScraper(PageWithoutNextData(session), OptionsFor(EasyScraper.Key, "taladro", "lijadora"), NullLogger<EasyScraper>.Instance);

        var error = await Assert.ThrowsAsync<ScrapeFailedException>(() => CollectAsync(scraper.ScrapeAsync()));

        Assert.Contains("No se pudo iniciar Chromium", error.Message);
    }

    // ---------- Imperial: búsqueda guiada de Oracle Commerce ----------

    private const string ImperialSearch = "/ccstore/v1/assembler/pages/Default/services/guidedsearch?Ntt=taladro&searchType=simple&Nrpp=36";
    private const string ImperialCategory = "/ccstore/v1/assembler/pages/Default/services/guidedsearch/_/N-1033696118?Nrpp=36&No=";

    private static HttpResponseMessage ImperialApi(HttpRequestMessage req) => req.RequestUri!.PathAndQuery switch
    {
        ImperialSearch => FakeHttpHandler.Ok(Fixture.Read("imperial-guidedsearch-redirect.json")),
        ImperialCategory + "0" => FakeHttpHandler.Ok(Fixture.Read("imperial-guidedsearch-results.json")),
        ImperialCategory + "36" => FakeHttpHandler.Ok(Fixture.Read("imperial-guidedsearch-results-page2.json")),
        _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound),
    };

    [Fact]
    public void OccGuidedSearch_toma_la_redireccion_y_no_las_facetas()
    {
        var json = Fixture.Read("imperial-guidedsearch-redirect.json");

        Assert.Equal("N-1033696118", OccGuidedSearch.RedirectNavigationState(json));
        Assert.Null(OccGuidedSearch.RedirectNavigationState(Fixture.Read("imperial-guidedsearch-results.json")));
        Assert.Equal(40, OccGuidedSearch.TotalRecords(Fixture.Read("imperial-guidedsearch-results.json")));
    }

    [Fact]
    public void Parser_lee_registros_anidados_del_assembler_con_ruta_o_accion_de_detalle()
    {
        var products = CapturedJsonProductParser.Parse(Fixture.Read("imperial-guidedsearch-results.json"), new Uri("https://www.imperial.cl"));

        Assert.Equal(2, products.Count);
        var gsb = products[0];
        Assert.Equal("IMP-1001-01", gsb.ExternalId);
        Assert.Equal("Taladro Percutor 650W GSB 13 RE", gsb.Title);
        Assert.Equal(49990m, gsb.Price);           // sku.activePrice
        Assert.Equal(64990m, gsb.ListPrice);
        Assert.True(gsb.IsAvailable);              // INSTOCK
        Assert.Equal("Bosch", gsb.Brand);
        Assert.Equal("https://www.imperial.cl/taladro-percutor-650w-gsb-13-re/product/IMP-1001", gsb.Url);

        var ddf = products[1];
        // Sin product.route: se usa detailsAction.recordState, sin los parámetros de Endeca.
        Assert.Equal("https://www.imperial.cl/taladro-inalambrico-18v-ddf485z/product/IMP-1002", ddf.Url);
        Assert.False(ddf.IsAvailable);             // OUTOFSTOCK
        Assert.Null(ddf.ListPrice);
    }

    [Fact]
    public async Task Imperial_resuelve_el_termino_sigue_la_redireccion_y_pagina_con_No()
    {
        var requested = new List<string>();
        var session = new FakeBrowserSession(_ => throw new InvalidOperationException("No debía usarse el navegador"));
        var services = With(session, req => { requested.Add(req.RequestUri!.PathAndQuery); return ImperialApi(req); });

        var products = await CollectAsync(new ImperialScraper(services, OptionsFor(ImperialScraper.Key, "taladro"), NullLogger<ImperialScraper>.Instance).ScrapeAsync());

        Assert.Equal(["IMP-1001-01", "IMP-1002-01", "IMP-1003-01"], products.Select(p => p.ExternalId));
        // totalNumRecs = 40: con 36 por página bastan 2 páginas.
        Assert.Equal([ImperialSearch, ImperialCategory + "0", ImperialCategory + "36"], requested);
    }

    [Fact]
    public async Task Imperial_pide_la_API_dentro_de_Chromium_si_HTTP_responde_403()
    {
        var session = new FakeBrowserSession(
            load: url => Page(url),
            get: url => new Uri(url).PathAndQuery switch
            {
                ImperialSearch => Fixture.Read("imperial-guidedsearch-redirect.json"),
                ImperialCategory + "0" => Fixture.Read("imperial-guidedsearch-results.json"),
                ImperialCategory + "36" => Fixture.Read("imperial-guidedsearch-results-page2.json"),
                _ => null,
            });
        var services = With(session, _ => Status(System.Net.HttpStatusCode.Forbidden));

        var products = await CollectAsync(new ImperialScraper(services, OptionsFor(ImperialScraper.Key, "taladro"), NullLogger<ImperialScraper>.Instance).ScrapeAsync());

        Assert.Equal(3, products.Count);
        Assert.Equal("https://www.imperial.cl/", Assert.Single(session.Loaded));   // solo la portada, por las cookies
        Assert.Equal(3, session.Fetched.Count);
    }

    [Fact]
    public async Task Imperial_acepta_un_estado_de_navegacion_como_busqueda()
    {
        var requested = new List<string>();
        var services = With(new FakeBrowserSession(_ => null), req => { requested.Add(req.RequestUri!.PathAndQuery); return ImperialApi(req); });
        var options = Options.Create(new ScraperOptions
        {
            Stores = { [ImperialScraper.Key] = new StoreScraperOptions { DelayBetweenRequestsMs = 0, MaxPagesPerQuery = 1, Queries = ["N-1033696118"] } },
        });

        var products = await CollectAsync(new ImperialScraper(services, options, NullLogger<ImperialScraper>.Instance).ScrapeAsync());

        Assert.Equal(2, products.Count);
        Assert.Equal([ImperialCategory + "0"], requested);
    }

    [Fact]
    public async Task Imperial_sin_API_recurre_a_la_pagina_en_Chromium()
    {
        var session = new FakeBrowserSession(url => Page("https://www.imperial.cl/taladros/category/000300030352", Fixture.Read("imperial-listing.html")));
        var services = With(session, _ => Status(System.Net.HttpStatusCode.NotFound));

        var products = await CollectAsync(new ImperialScraper(services, OptionsFor(ImperialScraper.Key, "taladro"), NullLogger<ImperialScraper>.Instance).ScrapeAsync());

        Assert.Equal("https://www.imperial.cl/search?Ntt=taladro", Assert.Single(session.Loaded));
        Assert.Equal(2, products.Count);
        Assert.Equal(49990m, products[0].Price);   // grilla renderizada, precio final prioritario
    }
}
