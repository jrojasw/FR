using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;
using PrecioHerramientas.Infrastructure.Maintenance;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Stores;
using PrecioHerramientas.Tests.Support;

namespace PrecioHerramientas.Tests;

public class TextEncodingTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    /// <summary>Daña un texto como lo hace leer UTF-8 con Windows-1252.</summary>
    private static string Mojibake(string text)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252).GetString(Encoding.UTF8.GetBytes(text));
    }

    // ---------- Reparador ----------

    [Theory]
    [InlineData("VÃ¡lvula", "Válvula")]
    [InlineData("Llave InalÃ¡mbrica", "Llave Inalámbrica")]
    [InlineData("Cargador baterÃ­a 20V", "Cargador batería 20V")]
    [InlineData("Esmeril 4 1/2â€³ 850W", "Esmeril 4 1/2″ 850W")]
    [InlineData("Ã‘andÃº - CaÃ±o 90Â°", "Ñandú - Caño 90°")]
    [InlineData("Taladro â€œProâ€\u009D â€“ ediciÃ³n", "Taladro “Pro” – edición")]
    public void Repara_los_ejemplos_reales(string broken, string expected) =>
        Assert.Equal(expected, TextRepair.Fix(broken));

    [Theory]
    [InlineData("Válvula de bola 1/2\"")]
    [InlineData("Llave Ñuñoa ñandú")]
    [InlineData("Hidrolavadora Kärcher K2 · 1400W")]
    [InlineData("Codo 90° PVC — ¿Pregunta? ¡Oferta!")]
    [InlineData("Esmeril 4 1/2″ 850W")]
    [InlineData("TALADRO PERCUTOR")]
    public void No_toca_texto_correcto(string text)
    {
        Assert.Equal(text, TextRepair.Fix(text));
        Assert.False(TextRepair.LooksBroken(text));
    }

    [Fact]
    public void Repara_texto_mezclado_y_danado_dos_veces()
    {
        Assert.Equal("Válvula inalámbrica", TextRepair.Fix("Válvula inalÃ¡mbrica"));
        Assert.Equal("Inalámbrica", TextRepair.Fix(Mojibake(Mojibake("Inalámbrica"))));
    }

    [Fact]
    public void Cualquier_texto_en_espanol_vuelve_igual_despues_de_danarse()
    {
        const string original = "Sierra Caladora Inalámbrica 18V con batería, maletín y 2 hojas — ñandú «Pro» 4 1/2″ 90° ¿ok? ¡sí! ÁÉÍÓÚÜÑ";
        Assert.Equal(original, TextRepair.Fix(Mojibake(original)));
    }

    // ---------- Lectura HTTP ----------

    [Fact]
    public void UTF8_declarado_como_ISO_8859_1_se_lee_como_UTF8() =>
        Assert.Equal("Válvula Ñandú 4 1/2″", HttpText.Decode(Encoding.UTF8.GetBytes("Válvula Ñandú 4 1/2″"), "iso-8859-1"));

    [Fact]
    public void Latin1_de_verdad_se_lee_con_su_codificacion() =>
        Assert.Equal("Válvula Ñandú", HttpText.Decode(Encoding.Latin1.GetBytes("Válvula Ñandú"), "ISO-8859-1"));

    [Fact]
    public void UTF8_con_BOM_no_deja_caracteres_extra() =>
        Assert.Equal("Válvula", HttpText.Decode([.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes("Válvula")], null));

    [Fact]
    public async Task Sodimac_que_declara_ISO_8859_1_pero_envia_UTF8_entrega_el_nombre_correcto()
    {
        var html = Fixture.Read("sodimac-search.html").Replace("Rotomartillo SDS Plus 800W HR2470", "Rotomartillo Inalámbrico Ñandú 4 1/2″");
        var handler = new FakeHttpHandler(req =>
        {
            var content = new ByteArrayContent(req.RequestUri!.Query.Contains("page=1") ? Encoding.UTF8.GetBytes(html) : Encoding.UTF8.GetBytes("<html>__NEXT_DATA__</html>"));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/html") { CharSet = "ISO-8859-1" };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        var services = new ServiceCollection()
            .AddKeyedSingleton<IPageFetcher>(PageFetcherKeys.Http, new HttpPageFetcher(new FakeHttpClientFactory(handler), NullLogger<HttpPageFetcher>.Instance))
            .AddKeyedSingleton<IPageFetcher>(PageFetcherKeys.Browser, new FakePageFetcher(_ => null))
            .BuildServiceProvider();
        var options = Options.Create(new ScraperOptions
        {
            Stores = { [SodimacScraper.Key] = new StoreScraperOptions { DelayBetweenRequestsMs = 0, MaxPagesPerQuery = 2, Queries = ["taladro"] } },
        });

        var products = new List<ScrapedProduct>();
        await foreach (var p in new SodimacScraper(services, options, NullLogger<SodimacScraper>.Instance).ScrapeAsync()) products.Add(p);

        Assert.Equal("Rotomartillo Inalámbrico Ñandú 4 1/2″", products[0].Title);
    }

    // ---------- Ingesta ----------

    [Fact]
    public async Task Un_nombre_con_tildes_y_ene_se_guarda_bien()
    {
        var store = await _test.AddStoreAsync("sodimac");

        await _test.IngestAsync(store, new ScrapedProduct
        {
            ExternalId = "1", Title = "Válvula de paso inalámbrica Ñandú 4 1/2″", Url = "https://x.example/1", Price = 9990,
            Brand = "Señor Herramientas", Category = "Gasfitería",
        });

        var offer = await _test.Db.Offers.Include(o => o.Product).ThenInclude(p => p.Category).SingleAsync();
        Assert.Equal("Válvula de paso inalámbrica Ñandú 4 1/2″", offer.Title);
        Assert.Equal("Válvula de paso inalámbrica Ñandú 4 1/2″", offer.Product.Name);
        Assert.Equal("Señor Herramientas", offer.BrandRaw);
        Assert.Equal("Gasfitería", offer.Product.Category!.Name);
        Assert.Equal("valvula-de-paso-inalambrica-nandu-4-1-2", offer.Product.Slug);
    }

    [Fact]
    public async Task Textos_que_llegan_danados_se_guardan_reparados()
    {
        var store = await _test.AddStoreAsync("sodimac");

        await _test.IngestAsync(store, new ScrapedProduct
        {
            ExternalId = "1", Title = "VÃ¡lvula InalÃ¡mbrica 4 1/2â€³", Url = "https://x.example/1", Price = 9990,
            Brand = "SeÃ±or", Category = "GasfiterÃ­a", Seller = "FerreterÃ­a Ã‘uÃ±oa",
        });

        var offer = await _test.Db.Offers.Include(o => o.Product).ThenInclude(p => p.Category).SingleAsync();
        Assert.Equal("Válvula Inalámbrica 4 1/2″", offer.Title);
        Assert.Equal("Válvula Inalámbrica 4 1/2″", offer.Product.Name);
        Assert.Equal("Señor", offer.BrandRaw);
        Assert.Equal("Gasfitería", offer.Product.Category!.Name);
        Assert.Equal("Ferretería Ñuñoa", offer.Seller);
        Assert.Equal("valvula-inalambrica-4-1-2", offer.Product.Slug);
    }

    // ---------- Reparación de datos guardados ----------

    private async Task<(Product Broken, Product Good)> SeedBrokenDataAsync()
    {
        var store = await _test.AddStoreAsync("sodimac");
        var now = _test.Clock.UtcNow;
        var goodBrand = new Brand { Name = "Kärcher", Slug = "karcher" };
        var brokenBrand = new Brand { Name = "KÃ¤rcher", Slug = "ka-rcher" };
        var broken = new Product
        {
            Name = "VÃ¡lvula InalÃ¡mbrica", Slug = "va-lvula-inala-mbrica", Model = "VAL100", Brand = brokenBrand,
            Category = new Category { Name = "GasfiterÃ­a", Slug = "gasfiteri-a" }, SearchText = "va lvula", CreatedAt = now, UpdatedAt = now,
        };
        var good = new Product { Name = "Hidrolavadora K2", Slug = "hidrolavadora-k2", Model = "K2", Brand = goodBrand, SearchText = "hidrolavadora", CreatedAt = now, UpdatedAt = now };
        var offer = new Offer
        {
            Store = store, Product = broken, ExternalId = "S-1", Url = "https://x.example/1", Title = "VÃ¡lvula InalÃ¡mbrica 4 1/2â€³",
            BrandRaw = "KÃ¤rcher", Price = 9990, IsAvailable = true, FirstSeenAt = now, LastSeenAt = now, LastPriceChangeAt = now,
        };
        offer.PriceHistory.Add(new PricePoint { Price = 10990, IsAvailable = true, CapturedAt = now.AddDays(-3) });
        offer.PriceHistory.Add(new PricePoint { Price = 9990, IsAvailable = true, CapturedAt = now });
        _test.Db.AddRange(good, offer);
        await _test.Db.SaveChangesAsync();
        return (broken, good);
    }

    [Fact]
    public async Task La_vista_previa_muestra_los_cambios_sin_guardar_nada()
    {
        var (broken, _) = await SeedBrokenDataAsync();

        var report = await new TextRepairService(_test.NewContext()).PreviewAsync();

        Assert.Equal(("products", "name", "VÃ¡lvula InalÃ¡mbrica", "Válvula Inalámbrica"),
            (report.Changes[0].Table, report.Changes[0].Field, report.Changes[0].Before, report.Changes[0].After));
        Assert.Contains(report.Changes, c => c.Field == "title" && c.After == "Válvula Inalámbrica 4 1/2″");
        Assert.Contains(report.Merges, m => m.Contains("Kärcher"));
        Assert.Equal("VÃ¡lvula InalÃ¡mbrica", (await _test.NewContext().Products.SingleAsync(p => p.Id == broken.Id)).Name);
    }

    [Fact]
    public async Task Aplicar_repara_productos_ofertas_marcas_y_categorias_sin_borrar_historial()
    {
        var (broken, _) = await SeedBrokenDataAsync();

        await new TextRepairService(_test.NewContext()).ApplyAsync();

        using var db = _test.NewContext();
        var product = await db.Products.Include(p => p.Brand).Include(p => p.Category).SingleAsync(p => p.Id == broken.Id);
        Assert.Equal("Válvula Inalámbrica", product.Name);
        Assert.Equal("valvula-inalambrica", product.Slug);
        Assert.Equal("valvula inalambrica karcher val100", product.SearchText);
        Assert.Equal("Kärcher", product.Brand!.Name);                  // se fusionó con la marca bien escrita
        Assert.Equal(1, await db.Brands.CountAsync(b => b.Slug == "karcher"));
        Assert.Equal(0, await db.Brands.CountAsync(b => b.Name.Contains("Ã")));
        Assert.Equal("Gasfitería", product.Category!.Name);
        var offer = await db.Offers.SingleAsync();
        Assert.Equal("Válvula Inalámbrica 4 1/2″", offer.Title);
        Assert.Equal("Kärcher", offer.BrandRaw);
        Assert.Equal(2, await db.PricePoints.CountAsync());          // historial intacto
        Assert.Equal(2, await db.Products.CountAsync());             // ningún producto borrado

        Assert.Equal(0, (await new TextRepairService(_test.NewContext()).PreviewAsync()).Count);   // nada pendiente
    }
}
