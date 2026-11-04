using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrecioHerramientas.Core.Queries;

namespace PrecioHerramientas.Web.Pages;

public class ProductModel(ICatalogQueries queries) : PageModel
{
    public ProductDetail Product { get; private set; } = null!;

    /// <summary>Una fila por tienda + vendedor + sucursal; el resto de las publicaciones, agrupadas.</summary>
    public IReadOnlyList<OfferGroup> Groups { get; private set; } = [];

    public OfferRow? BestOffer => Groups.Select(g => g.Primary).Where(r => r.Offer.IsAvailable).MinBy(r => r.Offer.Price);

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        var product = await queries.GetProductAsync(slug, ct);
        if (product is null) return NotFound();
        Product = product;
        Groups = OfferGrouping.Build(product.Offers);
        return Page();
    }
}
