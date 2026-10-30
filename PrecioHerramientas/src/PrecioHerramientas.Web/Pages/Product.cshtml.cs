using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrecioHerramientas.Core.Queries;

namespace PrecioHerramientas.Web.Pages;

public class ProductModel(ICatalogQueries queries) : PageModel
{
    public ProductDetail Product { get; private set; } = null!;

    public OfferView? BestOffer => Product.Offers.Where(o => o.IsAvailable).MinBy(o => o.Price);

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        var product = await queries.GetProductAsync(slug, ct);
        if (product is null) return NotFound();
        Product = product;
        return Page();
    }
}
