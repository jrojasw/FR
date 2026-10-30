using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrecioHerramientas.Core.Queries;

namespace PrecioHerramientas.Web.Pages;

public class SearchModel(ICatalogQueries queries) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Text { get; set; }
    [BindProperty(SupportsGet = true, Name = "marca")] public string? Brand { get; set; }
    [BindProperty(SupportsGet = true, Name = "categoria")] public string? Category { get; set; }
    [BindProperty(SupportsGet = true, Name = "tienda")] public string? Store { get; set; }
    [BindProperty(SupportsGet = true, Name = "min")] public decimal? MinPrice { get; set; }
    [BindProperty(SupportsGet = true, Name = "max")] public decimal? MaxPrice { get; set; }
    [BindProperty(SupportsGet = true, Name = "orden")] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true, Name = "pagina")] public int PageNumber { get; set; } = 1;

    public PagedResult<ProductListItem> Results { get; private set; } = new([], 0, 1, 24);
    public IReadOnlyList<FacetItem> Brands { get; private set; } = [];
    public IReadOnlyList<FacetItem> Categories { get; private set; } = [];
    public IReadOnlyList<StoreOverview> Stores { get; private set; } = [];

    public static readonly IReadOnlyList<(string Value, string Label)> SortOptions =
    [
        ("", "Relevancia"),
        ("precio-asc", "Menor precio"),
        ("precio-desc", "Mayor precio"),
        ("nombre", "Nombre"),
        ("nuevos", "Más recientes"),
    ];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Results = await queries.SearchAsync(new ProductSearchQuery
        {
            Text = Text,
            BrandSlug = Brand,
            CategorySlug = Category,
            StoreKey = Store,
            MinPrice = MinPrice,
            MaxPrice = MaxPrice,
            Sort = Sort switch
            {
                "precio-asc" => ProductSort.PriceAsc,
                "precio-desc" => ProductSort.PriceDesc,
                "nombre" => ProductSort.Name,
                "nuevos" => ProductSort.Newest,
                _ => ProductSort.Relevance,
            },
            Page = PageNumber,
            PageSize = 24,
        }, ct);
        Brands = await queries.GetBrandsAsync(ct);
        Categories = await queries.GetCategoriesAsync(ct);
        Stores = (await queries.GetStoresAsync(ct)).Where(s => s.IsActive && s.AvailableOffers > 0).ToList();
    }

    /// <summary>URL de esta búsqueda cambiando solo la página.</summary>
    public string PageUrl(int page)
    {
        var query = Request.Query
            .Where(kv => kv.Key != "pagina")
            .SelectMany(kv => kv.Value.Select(v => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(v ?? "")}"))
            .Append($"pagina={page}");
        return $"/buscar?{string.Join("&", query)}";
    }
}
