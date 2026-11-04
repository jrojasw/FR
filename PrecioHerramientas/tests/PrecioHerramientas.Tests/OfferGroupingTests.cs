using PrecioHerramientas.Core.Queries;

namespace PrecioHerramientas.Tests;

public class OfferGroupingTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static OfferView Offer(string store, string storeName, decimal price, decimal? list = null, string? seller = null,
        string? sku = null, string? branch = null, bool available = true, string title = "Lijadora excéntrica 320W TF2131256") =>
        new(store, storeName, null, $"https://{store}.example/{sku}", title, price, list, available, Now, Now, branch, seller, sku);

    [Fact]
    public void Caso_lijadora_Total_una_fila_por_tienda_y_vendedor_sin_repetir_Imperial()
    {
        var offers = new[]
        {
            Offer("sodimac", "Sodimac", 56_191, 83_990, "SODIMAC", "119001-1"),
            Offer("sodimac", "Sodimac", 56_191, 95_990, "SODIMAC", "119001-2"),
            // Publicación de Imperial dentro de sodimac.cl, igual a la de Imperial: se descarta.
            Offer("sodimac", "Sodimac", 80_490, null, "IMPERIAL", "119001-7", title: "Lijadora excentrica 320w velocidad variable tf2131256-7"),
            Offer("imperial", "Imperial", 80_490, null, null, "IMP-77", title: "Lijadora excentrica 320w velocidad variable tf2131256-7"),
        };

        var groups = OfferGrouping.Build(offers);

        Assert.Equal(2, groups.Count);
        var sodimac = groups[0];
        Assert.Equal("Sodimac", sodimac.Primary.StoreLabel);
        Assert.Null(sodimac.Primary.SellerNote);              // vende la propia tienda
        Assert.Equal(56_191, sodimac.Primary.Offer.Price);
        Assert.Single(sodimac.More);                          // el otro SKU de Sodimac, en "ver más"
        Assert.Equal("Imperial", groups[1].Primary.StoreLabel);
        Assert.Empty(groups[1].More);
    }

    [Fact]
    public void Imperial_vendiendo_en_Sodimac_a_otro_precio_se_muestra_como_via_Sodimac()
    {
        var groups = OfferGrouping.Build(
        [
            Offer("sodimac", "Sodimac", 78_990, seller: "Imperial S.A.", sku: "119001-7"),
            Offer("imperial", "Imperial", 80_490, sku: "IMP-77"),
        ]);

        Assert.Equal(["Imperial (vía Sodimac)", "Imperial"], groups.Select(g => g.Primary.StoreLabel));
        Assert.Null(groups[0].Primary.SellerNote);
    }

    [Fact]
    public void Un_vendedor_que_no_seguimos_se_indica_como_vendido_por()
    {
        var groups = OfferGrouping.Build(
        [
            Offer("sodimac", "Sodimac", 60_000, seller: "Ferretería Los Andes", sku: "A"),
            Offer("sodimac", "Sodimac", 58_000, seller: "Sodimac", sku: "B"),
        ]);

        Assert.Equal(2, groups.Count);   // vendedores distintos: filas distintas
        Assert.Equal("Sodimac", groups[0].Primary.StoreLabel);
        Assert.Null(groups[0].Primary.SellerNote);
        Assert.Equal("Sodimac", groups[1].Primary.StoreLabel);
        Assert.Equal("Vendido por Ferretería Los Andes", groups[1].Primary.SellerNote);
    }

    [Fact]
    public void Cada_sucursal_tiene_su_fila_y_la_de_stock_va_primero_en_el_grupo()
    {
        var groups = OfferGrouping.Build(
        [
            Offer("construmart", "Construmart", 45_990, branch: "Puente Alto", sku: "1"),
            Offer("construmart", "Construmart", 47_990, branch: "La Florida", sku: "1"),
            Offer("construmart", "Construmart", 40_000, branch: "La Florida", sku: "2", available: false),
        ]);

        Assert.Equal(["Puente Alto", "La Florida"], groups.Select(g => g.Primary.Offer.BranchName));
        var florida = groups[1];
        Assert.Equal(47_990, florida.Primary.Offer.Price);   // con stock, aunque la otra sea más barata
        Assert.False(Assert.Single(florida.More).Offer.IsAvailable);
    }
}
