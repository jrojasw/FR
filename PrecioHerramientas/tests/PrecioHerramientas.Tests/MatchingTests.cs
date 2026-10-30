using PrecioHerramientas.Core.Matching;
using PrecioHerramientas.Core.Scraping;

namespace PrecioHerramientas.Tests;

public class MatchingTests
{
    [Theory]
    [InlineData("BLACK+DECKER", null, "Black+Decker")]
    [InlineData("Black & Decker", null, "Black+Decker")]
    [InlineData("DEWALT", null, "DeWalt")]
    [InlineData(null, "Taladro percutor Makita HP1630", "Makita")]
    [InlineData("Genérico", "Hidrolavadora Karcher K2", "Kärcher")]
    [InlineData("Marca Nueva", null, "Marca Nueva")]
    [InlineData(null, "Taladro sin marca conocida", null)]
    public void BrandCatalog_unifica_variantes(string? raw, string? title, string? expected) =>
        Assert.Equal(expected, BrandCatalog.Resolve(raw, title));

    [Fact]
    public void BrandCatalog_no_confunde_palabras_dentro_de_otras() =>
        Assert.Null(BrandCatalog.Resolve(null, "Kit totalmente nuevo"));

    [Theory]
    [InlineData("Taladro Percutor Inalámbrico 20V DCD778D2 DeWalt", "DeWalt", "DCD778D2")]
    [InlineData("Esmeril Angular 4 1/2\" 850W GWS 850", "Bosch", "GWS850")]
    [InlineData("Rotomartillo SDS Plus 800W HR2470", "Makita", "HR2470")]
    [InlineData("Compresor 50 lts 2HP Modelo: TC-AC 200/50", "Einhell", "TCAC20050")]
    [InlineData("Martillo Carpintero 16oz Mango Fibra", "Truper", null)]
    [InlineData("Taladro 20V 2.0Ah 13mm 650W 1/2\"", null, null)]
    public void ModelExtractor_encuentra_el_numero_de_parte(string title, string? brand, string? expected) =>
        Assert.Equal(expected, ModelExtractor.Extract(title, brand));

    [Fact]
    public void ProductIdentity_prefiere_datos_estructurados()
    {
        var identity = ProductIdentity.From(new ScrapedProduct
        {
            ExternalId = "1", Title = "Taladro DCD778D2", Url = "https://x", Price = 1,
            Brand = "DEWALT", Model = "dcd-778d2", Ean = "7898935427473",
        });

        Assert.Equal("DeWalt", identity.Brand);
        Assert.Equal("DCD778D2", identity.Model);
        Assert.Equal("7898935427473", identity.Ean);
        Assert.Equal("dewalt:DCD778D2", identity.ModelKey);
    }
}
