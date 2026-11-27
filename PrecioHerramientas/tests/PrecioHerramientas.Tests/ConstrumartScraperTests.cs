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
    private const string SwitchUrl = "https://www.construmart.cl/catalogswitcher/switch/customergroup";

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
    /// Sitio simulado como el real (nov. 2026): la búsqueda redirige a la categoría; la página 1
    /// tiene la grilla y la 2 viene vacía. El modal ofrece las regiones en mayúsculas y, según la
    /// elegida, sus tiendas con id numérico.
    /// </summary>
    private static FakeBrowserSession Site()
    {
        var session = new FakeBrowserSession(url =>
            url.Contains("catalogsearch") ? new BrowserPage(Category, Fixture.Read("construmart-listing.html"), [])
            : url.Contains("p=2") ? new BrowserPage(url, "<html><body><div class='message notice'>No hay más productos</div></body></html>", [])
            : new BrowserPage(url, "<html><body>Elige tu tienda</body></html>", []));
        session.Options = (selector, chosen) => selector.Contains("region")
            ? [new("", "Selecciona tu región"), new("VALPARAISO", "VALPARAISO"), new("REGIÓN METROPOLITANA DE SANTIAGO", "REGIÓN METROPOLITANA DE SANTIAGO")]
            : chosen.Values.Contains("REGIÓN METROPOLITANA DE SANTIAGO")
                ? [new("", "Selecciona tu tienda"), new("14", "LAS CONDES"), new("38", "PEÑAFLOR")]
                : chosen.Values.Contains("VALPARAISO") ? [new("40", "VIÑA DEL MAR")] : [new("", "Selecciona tu tienda")];
        return session;
    }

    [Fact]
    public async Task Lee_solo_la_grilla_de_resultados_y_no_el_widget_de_recomendaciones()
    {
        using var document = await BrowsingContext.New(Configuration.Default)
            .OpenAsync(req => req.Content(Fixture.Read("construmart-listing.html")).Address(Category));

        var parsed = PrecioHerramientas.Scrapers.Parsing.HtmlListingParser.Parse(document, new Uri("https://www.construmart.cl"), ConstrumartScraper.Grid);

        // El widget trae "Set de Vasos Cerveceros" y "Pino Seco"; la tarjeta "Sin Stock" no tiene precio.
        Assert.Equal(["245825", "233715", "245799"], parsed.Select(p => p.ExternalId));
        var taladro = parsed[0];
        Assert.Equal("Taladro Atornillador Inalámbrico 13 mm 20 V + 2 Baterías 2000 mAh + Cargador", taladro.Title);
        Assert.Equal(39990m, taladro.Price);          // data-price-amount
        Assert.Equal(69990m, taladro.ListPrice);      // oldPrice
        Assert.Equal("https://www.construmart.cl/taladro-atornillador-inalambrico-13-245825", taladro.Url);
        Assert.StartsWith("https://www.construmart.cl/media/catalog/product/", taladro.ImageUrl);
        Assert.True(taladro.IsAvailable);
        Assert.Null(parsed[1].ListPrice);             // sin precio anterior
    }

    [Fact]
    public async Task Fija_cada_sucursal_con_el_POST_del_sitio_y_corre_las_busquedas_en_ella()
    {
        var session = Site();
        var scraper = Scraper(session, OptionsWith(new BranchOptions { Regions = ["Metropolitana"] }, "taladro", "atornillador"));

        var products = await CollectAsync(scraper.ScrapeAsync());

        // 3 productos × 2 sucursales (Las Condes y Peñaflor); Viña del Mar no es de la región.
        Assert.Equal(6, products.Count);
        Assert.Equal(["14", "38"], products.Select(p => p.BranchCode).Distinct());
        Assert.All(products.Where(p => p.BranchCode == "38"), p => Assert.Equal("Peñaflor", p.BranchName));   // nombre legible

        // Una vez por sucursal, con el mismo POST que hace el sitio; sin abrir el modal para elegir.
        Assert.Equal([$"post:{SwitchUrl}?customer_group_id=14", $"post:{SwitchUrl}?customer_group_id=38"],
            session.Actions.Where(a => a.StartsWith("post:")));
        Assert.DoesNotContain(session.Actions, a => a.StartsWith("choose:") && (a.EndsWith("=14") || a.EndsWith("=38")));
        Assert.Equal(4, session.Loaded.Count(u => u.Contains("catalogsearch")));
        Assert.Contains(session.Loaded, u => u == Category + "?p=2");   // pagina sobre la categoría
        Assert.Equal(1, session.Opened);
        Assert.Equal(1, session.Disposed);
    }

    [Fact]
    public async Task La_region_se_elige_en_el_select_del_modal_antes_que_en_el_del_encabezado()
    {
        var session = Site();

        await CollectAsync(Scraper(session, OptionsWith(new BranchOptions { Regions = ["Metropolitana"] }, "taladro")).ScrapeAsync());

        var regionChoice = Assert.Single(session.Actions, a => a.StartsWith("choose:") && a.Contains("REGIÓN METROPOLITANA"));
        // La lista empieza por el select del modal; el del encabezado está dentro de un panel cerrado.
        Assert.StartsWith("choose:#region-selector-popup,", regionChoice);
    }

    [Fact]
    public async Task Si_el_POST_falla_usa_el_modal()
    {
        var session = Site();
        session.PostResponse = (_, _) => new FormPostResult(500, "error");

        var products = await CollectAsync(Scraper(session, OptionsWith(new BranchOptions { Regions = ["Metropolitana"] }, "taladro")).ScrapeAsync());

        Assert.Equal(6, products.Count);
        Assert.Contains(session.Actions, a => a.StartsWith("choose:#store-selector-popup") && a.EndsWith("=14"));
        Assert.Contains(session.Actions, a => a.StartsWith("click:#enter-store-button"));
    }

    [Fact]
    public async Task Con_lista_fija_y_cookie_conocida_no_usa_ni_POST_ni_modal()
    {
        var session = Site();
        var branches = new BranchOptions
        {
            Regions = ["Metropolitana"],
            CookieName = "persistent_customer_group_id",
            List =
            [
                new BranchDefinition { Id = "14", Name = "Las Condes", Region = "Metropolitana" },
                new BranchDefinition { Id = "40", Name = "Viña del Mar", Region = "Valparaíso" },
            ],
        };

        var products = await CollectAsync(Scraper(session, OptionsWith(branches, "taladro")).ScrapeAsync());

        Assert.Equal(3, products.Count);
        Assert.All(products, p => Assert.Equal("14", p.BranchCode));
        Assert.Contains("cookie:persistent_customer_group_id=14", session.Actions);
        Assert.DoesNotContain(session.Actions, a => a.StartsWith("choose:") || a.StartsWith("click:") || a.StartsWith("post:"));
    }

    [Fact]
    public async Task Sin_selector_de_sucursal_usa_los_precios_generales_en_vez_de_fallar()
    {
        var session = Site();
        session.Options = (_, _) => [];   // el sitio ya no ofrece elegir sucursal

        var products = await CollectAsync(Scraper(session, OptionsWith(new BranchOptions { Regions = ["Metropolitana"] }, "taladro")).ScrapeAsync());

        Assert.Equal(3, products.Count);
        Assert.All(products, p => Assert.Null(p.BranchCode));
        Assert.DoesNotContain(session.Actions, a => a.StartsWith("post:"));
    }

    [Fact]
    public async Task Si_Construmart_bloquea_el_navegador_lo_dice_claramente()
    {
        var session = new FakeBrowserSession(url => new BrowserPage(url,
            "<html><head><title>403 Forbidden</title></head><body><h1>Error 403 Forbidden</h1><h3>Error 54113</h3></body></html>", []));

        var error = await Assert.ThrowsAsync<ScrapeFailedException>(() =>
            CollectAsync(Scraper(session, OptionsWith(new BranchOptions { Regions = ["Metropolitana"] }, "taladro")).ScrapeAsync()));

        Assert.Contains("403", error.Message);
        Assert.DoesNotContain("sucursales para", error.Message);
    }

    [Theory]
    [InlineData("#a, #b", new[] { "#a", "#b" })]
    [InlineData("button:has-text('Hola, chao'), #x", new[] { "button:has-text('Hola, chao')", "#x" })]
    [InlineData("select[name*='a,b' i]", new[] { "select[name*='a,b' i]" })]
    public void Las_listas_de_selectores_se_separan_sin_cortar_textos(string list, string[] expected) =>
        Assert.Equal(expected, SelectorList.Split(list));
}
