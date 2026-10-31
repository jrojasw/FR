using Microsoft.EntityFrameworkCore;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Tests.Support;

namespace PrecioHerramientas.Tests;

public class PriceIngestionServiceTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    private static ScrapedProduct Item(string id, string title, decimal price, string? brand = null, string? model = null,
        string? ean = null, bool available = true, decimal? listPrice = null) => new()
    {
        ExternalId = id, Title = title, Url = $"https://tienda.example/{id}", Price = price, Brand = brand, Model = model,
        Ean = ean, IsAvailable = available, ListPrice = listPrice, Category = "Herramientas/Taladros",
    };

    [Fact]
    public async Task Agrupa_por_EAN_entre_tiendas()
    {
        var a = await _test.AddStoreAsync("a");
        var b = await _test.AddStoreAsync("b");

        await _test.IngestAsync(a, Item("1", "Taladro DeWalt DCD778D2", 189990, "DeWalt", ean: "7898935427473"));
        await _test.IngestAsync(b, Item("X9", "TALADRO PERCUTOR 20V", 179990, "Dewalt", ean: "07898935427473"));

        var product = await _test.Db.Products.Include(p => p.Offers).SingleAsync();
        Assert.Equal(2, product.Offers.Count);
        Assert.Equal(179990m, product.BestPrice);
        Assert.Equal(2, product.OfferCount);
        Assert.Equal("taladro-dewalt-dcd778d2", product.Slug);
        Assert.Equal("Taladros", (await _test.Db.Categories.SingleAsync()).Name);
    }

    [Fact]
    public async Task Agrupa_por_marca_y_modelo_y_por_modelo_dentro_del_titulo()
    {
        var a = await _test.AddStoreAsync("a");
        var b = await _test.AddStoreAsync("b");
        var c = await _test.AddStoreAsync("c");

        await _test.IngestAsync(a, Item("1", "Rotomartillo SDS Plus 800W", 149990, "MAKITA", "HR-2470"));
        // Sin marca ni modelo estructurados: se deducen del título.
        await _test.IngestAsync(b, Item("2", "Rotomartillo Makita HR2470 800W", 144990));
        // Modelo con otra variante de escritura, solo dentro del título.
        await _test.IngestAsync(c, Item("3", "Makita Rotomartillo SDS-Plus (HR 2470) 800 W", 139990, "Makita"));

        var product = await _test.Db.Products.SingleAsync();
        Assert.Equal("HR2470", product.Model);
        Assert.Equal(3, product.OfferCount);
        Assert.Equal(139990m, product.BestPrice);
    }

    [Fact]
    public async Task No_agrupa_mismo_modelo_de_distinta_marca()
    {
        var a = await _test.AddStoreAsync("a");

        await _test.IngestAsync(a,
            Item("1", "Taladro X1000", 10000, "Bosch", "X1000"),
            Item("2", "Taladro X1000", 12000, "Makita", "X1000"));

        Assert.Equal(2, await _test.Db.Products.CountAsync());
        Assert.Equal(2, (await _test.Db.Products.Select(p => p.Slug).Distinct().ToListAsync()).Count);
    }

    [Fact]
    public async Task Registra_historial_solo_cuando_cambia_el_precio()
    {
        var a = await _test.AddStoreAsync("a");

        await _test.IngestAsync(a, Item("1", "Esmeril GWS850", 54990, "Bosch", listPrice: 59990));
        _test.Clock.UtcNow = _test.Clock.UtcNow.AddHours(6);
        await _test.IngestAsync(a, Item("1", "Esmeril GWS850", 54990, "Bosch", listPrice: 59990));
        _test.Clock.UtcNow = _test.Clock.UtcNow.AddHours(6);
        var stats = await _test.IngestAsync(a, Item("1", "Esmeril GWS850", 49990, "Bosch", listPrice: 59990));

        Assert.Equal(1, stats.PricesChanged);
        var offer = await _test.Db.Offers.Include(o => o.PriceHistory).SingleAsync();
        Assert.Equal(49990m, offer.Price);
        Assert.Equal(54990m, offer.PreviousPrice);
        Assert.Equal(_test.Clock.UtcNow, offer.LastSeenAt);
        Assert.Equal([54990m, 49990m], offer.PriceHistory.OrderBy(p => p.CapturedAt).Select(p => p.Price));
    }

    [Fact]
    public async Task Marca_agotadas_las_ofertas_que_desaparecen()
    {
        var a = await _test.AddStoreAsync("a");
        await _test.IngestAsync(a,
            Item("1", "Producto A1234", 1000, "Bosch"),
            Item("2", "Producto B1234", 2000, "Bosch"),
            Item("3", "Producto C1234", 3000, "Bosch"));

        _test.Clock.UtcNow = _test.Clock.UtcNow.AddHours(6);
        var stats = await _test.IngestAsync(a, Item("1", "Producto A1234", 1000, "Bosch"), Item("2", "Producto B1234", 2000, "Bosch"));

        Assert.Equal(1, stats.OffersMarkedUnavailable);
        var gone = await _test.Db.Offers.Include(o => o.Product).Include(o => o.PriceHistory).SingleAsync(o => o.ExternalId == "3");
        Assert.False(gone.IsAvailable);
        Assert.Equal(0, gone.Product.OfferCount);
        Assert.Null(gone.Product.BestPrice);
        Assert.Equal(2, gone.PriceHistory.Count);
    }

    [Fact]
    public async Task No_marca_agotadas_si_la_recoleccion_trajo_muy_pocos_productos()
    {
        var a = await _test.AddStoreAsync("a");
        await _test.IngestAsync(a,
            Item("1", "Producto A1234", 1000, "Bosch"),
            Item("2", "Producto B1234", 2000, "Bosch"),
            Item("3", "Producto C1234", 3000, "Bosch"));

        _test.Clock.UtcNow = _test.Clock.UtcNow.AddHours(6);
        var stats = await _test.IngestAsync(a, Item("1", "Producto A1234", 1000, "Bosch"));

        Assert.Equal(0, stats.OffersMarkedUnavailable);
        Assert.Equal(3, await _test.Db.Offers.CountAsync(o => o.IsAvailable));
    }

    [Fact]
    public async Task Descarta_productos_invalidos_y_duplicados()
    {
        var a = await _test.AddStoreAsync("a");

        var stats = await _test.IngestAsync(a,
            Item("1", "Producto válido Z900", 1000),
            Item("1", "Producto válido Z900", 900),
            Item("2", "Sin precio", 0),
            Item("", "Sin id", 1000));

        Assert.Equal(4, stats.ItemsFound);
        Assert.Equal(2, stats.ItemsRejected);
        var offer = await _test.Db.Offers.SingleAsync();
        Assert.Equal(900m, offer.Price);
    }

    [Fact]
    public async Task Recorta_campos_demasiado_largos_en_vez_de_fallar()
    {
        var a = await _test.AddStoreAsync("a");
        var item = Item(new string('9', 250), "Taladro " + new string('x', 700) + " DCD778D2", 1000, brand: new string('B', 300), model: string.Concat(Enumerable.Repeat("M1", 150))) with
        {
            Url = "https://tienda.example/" + new string('u', 1500),
            ImageUrl = "https://img.example/" + new string('i', 1500),
            Ean = new string('7', 300),
            Category = "Herramientas/" + new string('c', 400),
        };

        var stats = await _test.IngestAsync(a, item);

        Assert.Equal(0, stats.ItemsRejected);
        var offer = await _test.Db.Offers.Include(o => o.Product).ThenInclude(p => p.Brand).SingleAsync();
        Assert.Equal(FieldLimits.ExternalId, offer.ExternalId.Length);
        Assert.Equal(FieldLimits.OfferTitle, offer.Title.Length);
        Assert.Equal(FieldLimits.Url, offer.Url.Length);
        Assert.Equal(FieldLimits.Url, offer.ImageUrl!.Length);
        Assert.Equal(FieldLimits.BrandRaw, offer.BrandRaw!.Length);
        Assert.Equal(FieldLimits.ModelRaw, offer.ModelRaw!.Length);
        Assert.Equal(FieldLimits.OfferEan, offer.Ean!.Length);
        Assert.True(offer.Product.Name.Length <= FieldLimits.ProductName);
        Assert.True(offer.Product.Model!.Length <= FieldLimits.Model);
        Assert.True(offer.Product.SearchText.Length <= FieldLimits.SearchText);
        Assert.True(offer.Product.Brand!.Name.Length <= FieldLimits.BrandName);
        Assert.True((await _test.Db.Categories.SingleAsync()).Name.Length <= FieldLimits.CategoryName);
    }

    [Fact]
    public async Task Misma_publicacion_en_varias_sucursales_es_un_producto_con_una_oferta_por_sucursal()
    {
        var construmart = await _test.AddStoreAsync("construmart");
        var easy = await _test.AddStoreAsync("easy");

        // Sin marca ni modelo reconocibles: el agrupamiento entre sucursales sale del SKU.
        await _test.IngestAsync(construmart,
            Item("1004521", "Set de brocas 5 piezas", 7990) with { BranchCode = "CM-MAIPU", BranchName = "Maipú" },
            Item("1004521", "Set de brocas 5 piezas", 7490) with { BranchCode = "CM-LAFLORIDA", BranchName = "La Florida" });
        await _test.IngestAsync(construmart,
            Item("1004521", "Set de brocas 5 piezas", 6990) with { BranchCode = "CM-PUENTE", BranchName = "Puente Alto" },
            Item("1004521", "Set de brocas 5 piezas", 7990) with { BranchCode = "CM-MAIPU", BranchName = "Maipú" },
            Item("1004521", "Set de brocas 5 piezas", 7490) with { BranchCode = "CM-LAFLORIDA", BranchName = "La Florida" });
        await _test.IngestAsync(easy, Item("E-1", "Set de brocas 5 piezas", 8990));

        var products = await _test.Db.Products.Include(p => p.Offers).ToListAsync();
        var brocas = Assert.Single(products, p => p.Offers.Any(o => o.StoreId == construmart.Id));
        Assert.Equal(3, brocas.Offers.Count(o => o.StoreId == construmart.Id));
        Assert.Equal(["CM-LAFLORIDA", "CM-MAIPU", "CM-PUENTE"], brocas.Offers.Where(o => o.StoreId == construmart.Id).Select(o => o.BranchCode).Order());
        Assert.Equal(6990m, brocas.BestPrice);          // el menor entre sucursales
        Assert.Equal("Puente Alto", brocas.Offers.Single(o => o.BranchCode == "CM-PUENTE").BranchName);
    }

    [Fact]
    public async Task Cantidad_de_tiendas_cuenta_tiendas_y_no_sucursales()
    {
        var construmart = await _test.AddStoreAsync("construmart");
        var easy = await _test.AddStoreAsync("easy");

        await _test.IngestAsync(construmart,
            Item("1", "Taladro DeWalt DCD778D2", 180000, "DeWalt", "DCD778D2") with { BranchCode = "A" },
            Item("1", "Taladro DeWalt DCD778D2", 175000, "DeWalt", "DCD778D2") with { BranchCode = "B" });
        await _test.IngestAsync(easy, Item("E-9", "Taladro DeWalt DCD778D2", 189990, "DeWalt", "DCD778D2"));

        var product = await _test.Db.Products.SingleAsync();
        Assert.Equal(2, product.OfferCount);     // Construmart y Easy, aunque haya 3 ofertas
        Assert.Equal(175000m, product.BestPrice);
    }
}
