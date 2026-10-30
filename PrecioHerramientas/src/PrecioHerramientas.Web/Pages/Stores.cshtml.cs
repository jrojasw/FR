using Microsoft.AspNetCore.Mvc.RazorPages;
using PrecioHerramientas.Core.Queries;

namespace PrecioHerramientas.Web.Pages;

public class StoresModel(ICatalogQueries queries) : PageModel
{
    public IReadOnlyList<StoreOverview> Stores { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) =>
        Stores = (await queries.GetStoresAsync(ct)).Where(s => s.IsActive).ToList();
}
