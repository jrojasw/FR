using PrecioHerramientas.Core.Queries;
using PrecioHerramientas.Infrastructure.Queries;

namespace PrecioHerramientas.Tests;

public class PriceHistoryMergeTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static PricePointView P(int day, decimal price, bool available = true) => new(T0.AddDays(day), price, available);

    [Fact]
    public void Una_linea_por_tienda_sigue_el_menor_precio_con_stock_entre_sucursales()
    {
        var maipu = new List<PricePointView> { P(0, 10_000), P(5, 9_000), P(10, 9_000) };
        var florida = new List<PricePointView> { P(0, 9_500), P(3, 9_500, available: false), P(8, 8_500), P(10, 8_500) };

        var merged = CatalogQueries.LowestAcross([maipu, florida]);

        Assert.Equal(
            [
                P(0, 9_500),    // La Florida es la más barata
                P(3, 10_000),   // La Florida sin stock: vale Maipú
                P(5, 9_000),    // Maipú baja
                P(8, 8_500),    // La Florida vuelve con stock y más barata
                P(10, 8_500),   // se extiende hasta el último dato
            ],
            merged);
    }

    [Fact]
    public void Sin_stock_en_ninguna_sucursal_queda_marcado_como_sin_stock()
    {
        var merged = CatalogQueries.LowestAcross([[P(0, 5_000, false)], [P(0, 6_000, false)]]);

        Assert.Equal([P(0, 5_000, false)], merged);
    }
}
