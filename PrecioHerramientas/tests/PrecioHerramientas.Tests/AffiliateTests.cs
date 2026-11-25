using System.Net;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Affiliates;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Infrastructure.Repositories;
using PrecioHerramientas.Tests.Support;

namespace PrecioHerramientas.Tests;

public class AffiliateTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    private sealed class StaticOptions(AffiliateOptions value) : IOptionsMonitor<AffiliateOptions>
    {
        public AffiliateOptions CurrentValue => value;
        public AffiliateOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<AffiliateOptions, string?> listener) => null;
    }

    private static AffiliateLinkService Links(Dictionary<string, StoreAffiliateOptions> stores) => new(
        [new DirectLinkBuilder(), new AwinLinkBuilder(), new QueryParametersLinkBuilder()],
        new StaticOptions(new AffiliateOptions { Stores = new(stores, StringComparer.OrdinalIgnoreCase) }),
        NullLogger<AffiliateLinkService>.Instance);

    // ---------- Links por programa ----------

    [Fact]
    public void Awin_arma_el_link_con_la_url_codificada_y_el_clickref()
    {
        var links = Links(new() { ["paris"] = new() { Program = "Awin", MerchantId = "15130", AffiliateId = "987654" } });

        var link = links.BuildLink("paris", "https://www.paris.cl/taladro-dewalt-dcd778d2.html?color=amarillo&talla=1", "4821");

        Assert.Equal(
            "https://www.awin1.com/cread.php?awinmid=15130&awinaffid=987654&clickref=4821"
            + "&ued=https%3A%2F%2Fwww.paris.cl%2Ftaladro-dewalt-dcd778d2.html%3Fcolor%3Damarillo%26talla%3D1",
            link);
    }

    [Fact]
    public void QueryParameters_agrega_el_tracking_conserva_la_url_y_no_duplica()
    {
        var links = Links(new()
        {
            ["mercadolibre"] = new()
            {
                Program = "QueryParameters",
                Parameters = new() { ["matt_tool"] = "12345678", ["matt_word"] = "precioherramientas", ["ref"] = "{clickref}" },
            },
        });

        var link = links.BuildLink("mercadolibre", "https://articulo.mercadolibre.cl/MLC-123456-taladro-_JM?matt_tool=viejo#reviews", "77");

        var uri = new Uri(link);
        var query = HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("https://articulo.mercadolibre.cl/MLC-123456-taladro-_JM", uri.GetLeftPart(UriPartial.Path));
        Assert.Equal("12345678", query["matt_tool"]);          // reemplaza el que ya traía, sin duplicar
        Assert.Single(query.GetValues("matt_tool")!);
        Assert.Equal("precioherramientas", query["matt_word"]);
        Assert.Equal("77", query["ref"]);                     // {clickref} → id del clic
        Assert.Equal("#reviews", uri.Fragment);
    }

    [Theory]
    [InlineData("easy")]           // tienda sin entrada en la configuración
    [InlineData("sodimac")]        // "Direct" explícito
    public void Tiendas_sin_programa_van_a_la_url_normal(string store)
    {
        var links = Links(new() { ["sodimac"] = new() { Program = "Direct" } });

        Assert.Equal("https://www.sodimac.cl/sodimac-cl/product/1/x/1-7", links.BuildLink(store, "https://www.sodimac.cl/sodimac-cl/product/1/x/1-7", "5"));
    }

    [Theory]
    [InlineData("Awin", "", "987654")]          // falta el id de la tienda en Awin
    [InlineData("Awin", "15130", "")]           // falta el id de afiliado
    [InlineData("QueryParameters", "", "")]     // sin parámetros
    [InlineData("ProgramaInventado", "1", "2")] // programa desconocido
    public void Configuracion_incompleta_usa_el_link_directo_en_vez_de_uno_roto(string program, string mid, string affid)
    {
        var links = Links(new() { ["paris"] = new() { Program = program, MerchantId = mid, AffiliateId = affid } });

        Assert.Equal("https://www.paris.cl/p.html", links.BuildLink("paris", "https://www.paris.cl/p.html", "1"));
    }

    // ---------- Privacidad ----------

    [Theory]
    [InlineData("190.22.135.201", "190.22.135.0")]
    [InlineData("::ffff:190.22.135.201", "190.22.135.0")]          // IPv4 dentro de IPv6
    [InlineData("2800:150:11b:1d00:a1b2:c3d4:e5f6:1234", "2800:150:11b::")]
    public void La_IP_se_guarda_anonimizada(string ip, string expected) =>
        Assert.Equal(expected, ClickPrivacy.AnonymizeIp(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)", true)]
    [InlineData("facebookexternalhit/1.1", true)]
    [InlineData("WhatsApp/2.23.20.0", true)]
    [InlineData("curl/8.5.0", true)]
    [InlineData("", true)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1", false)]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36", false)]
    public void Los_robots_no_se_cuentan(string userAgent, bool isBot) =>
        Assert.Equal(isBot, ClickPrivacy.IsBot(userAgent));

    // ---------- Registro del clic ----------

    private const string Chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private async Task<(Store Store, Offer Offer)> SeedAsync()
    {
        var store = await _test.AddStoreAsync("paris");
        var product = new Product { Name = "Taladro DCD778D2", Slug = "taladro-dcd778d2", CreatedAt = _test.Clock.UtcNow, UpdatedAt = _test.Clock.UtcNow };
        var offer = new Offer
        {
            Store = store, Product = product, ExternalId = "P-1", Url = "https://www.paris.cl/taladro.html", Title = "Taladro",
            Price = 99990, IsAvailable = true, FirstSeenAt = _test.Clock.UtcNow, LastSeenAt = _test.Clock.UtcNow, LastPriceChangeAt = _test.Clock.UtcNow,
        };
        _test.Db.Offers.Add(offer);
        await _test.Db.SaveChangesAsync();
        return (store, offer);
    }

    private ClickRedirectService Service(Dictionary<string, StoreAffiliateOptions> stores)
    {
        var db = _test.NewContext();
        return new ClickRedirectService(new ClickRepository(db), new UnitOfWork(db), Links(stores), _test.Clock);
    }

    [Fact]
    public async Task El_clic_se_registra_y_redirige_al_link_de_afiliado_con_su_id()
    {
        var (store, offer) = await SeedAsync();
        var service = Service(new() { ["paris"] = new() { Program = "Awin", MerchantId = "15130", AffiliateId = "987654" } });

        var url = await service.ForOfferAsync(offer.Id, new ClickVisitor(IPAddress.Parse("190.22.135.201"), "https://precios.example/producto/taladro-dcd778d2", Chrome));

        var click = await _test.Db.Clicks.AsNoTracking().SingleAsync();
        Assert.Equal(offer.Id, click.OfferId);
        Assert.Equal(offer.ProductId, click.ProductId);
        Assert.Equal(store.Id, click.StoreId);
        Assert.Equal(_test.Clock.UtcNow, click.ClickedAt);
        Assert.Equal("190.22.135.0", click.AnonymizedIp);
        Assert.Equal("https://precios.example/producto/taladro-dcd778d2", click.Referer);
        Assert.StartsWith("https://www.awin1.com/cread.php?awinmid=15130&awinaffid=987654&clickref=" + click.Id + "&ued=", url);
    }

    [Fact]
    public async Task Un_robot_es_redirigido_pero_no_se_cuenta()
    {
        var (_, offer) = await SeedAsync();

        var url = await Service(new()).ForOfferAsync(offer.Id, new ClickVisitor(null, null, "Googlebot/2.1"));

        Assert.Equal("https://www.paris.cl/taladro.html", url);
        Assert.Empty(_test.Db.Clicks);
    }

    [Fact]
    public async Task Oferta_inexistente_no_redirige_ni_registra()
    {
        await SeedAsync();

        Assert.Null(await Service(new()).ForOfferAsync(99999, new ClickVisitor(null, null, Chrome)));
        Assert.Empty(_test.Db.Clicks);
    }

    [Fact]
    public async Task Clic_a_la_portada_de_una_tienda_registra_solo_la_tienda()
    {
        var (store, _) = await SeedAsync();

        var url = await Service(new()).ForStoreAsync("paris", new ClickVisitor(null, "https://precios.example/tiendas", Chrome));

        Assert.Equal(store.BaseUrl, url);
        var click = await _test.Db.Clicks.AsNoTracking().SingleAsync();
        Assert.Null(click.OfferId);
        Assert.Null(click.ProductId);
        Assert.Equal(store.Id, click.StoreId);
        Assert.Null(await Service(new()).ForStoreAsync("no-existe", new ClickVisitor(null, null, Chrome)));
    }

    [Fact]
    public async Task Referer_muy_largo_se_recorta()
    {
        var (_, offer) = await SeedAsync();

        await Service(new()).ForOfferAsync(offer.Id, new ClickVisitor(null, "https://precios.example/?" + new string('x', 900), Chrome));

        Assert.Equal(FieldLimits.Referer, (await _test.Db.Clicks.AsNoTracking().SingleAsync()).Referer!.Length);
    }
}
