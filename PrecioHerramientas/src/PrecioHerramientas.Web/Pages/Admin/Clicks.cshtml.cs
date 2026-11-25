using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrecioHerramientas.Core.Queries;

namespace PrecioHerramientas.Web.Pages.Admin;

/// <summary>Clics enviados a cada tienda y productos más clicados (protegida por BasicAuth en Program.cs).</summary>
public class ClicksModel(IClickReports reports) : PageModel
{
    public static readonly IReadOnlyList<(int Days, string Label)> Periods = [(1, "Hoy"), (7, "7 días"), (30, "30 días"), (0, "Todo")];

    /// <summary>Días hacia atrás; 0 = todo el historial.</summary>
    [BindProperty(SupportsGet = true, Name = "dias")]
    public int Days { get; set; } = 30;

    public ClickSummary Summary { get; private set; } = new(0, [], []);

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (!Periods.Any(p => p.Days == Days)) Days = 30;
        DateTime? since = Days == 0 ? null : DateTime.UtcNow.Date.AddDays(1 - Days);
        Summary = await reports.GetSummaryAsync(since, topProducts: 50, ct);
    }
}
