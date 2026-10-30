using Microsoft.AspNetCore.Mvc.RazorPages;
using PrecioHerramientas.Core.Queries;

namespace PrecioHerramientas.Web.Pages;

public class IndexModel(ICatalogQueries queries) : PageModel
{
    public IReadOnlyList<PriceDrop> Drops { get; private set; } = [];
    public IReadOnlyList<ProductListItem> Popular { get; private set; } = [];
    public IReadOnlyList<FacetItem> Categories { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Drops = await queries.GetRecentPriceDropsAsync(8, 7, ct);
        Popular = (await queries.SearchAsync(new ProductSearchQuery { PageSize = 12 }, ct)).Items;
        Categories = (await queries.GetCategoriesAsync(ct)).OrderByDescending(c => c.Count).Take(12).ToList();
    }
}
