using Microsoft.AspNetCore.Mvc.RazorPages;
using PrecioHerramientas.Core.Services;

namespace PrecioHerramientas.Web.Pages.Admin;

/// <summary>
/// Repara tildes y eñes dañadas en los datos guardados. GET muestra qué cambiaría (sin
/// guardar); POST lo aplica. Protegida por BasicAuth en Program.cs.
/// </summary>
public class RepairTextModel(ITextRepairService repair) : PageModel
{
    public TextRepairReport Report { get; private set; } = new([], []);

    /// <summary>Lo que se aplicó en este POST; null en un GET.</summary>
    public TextRepairReport? Applied { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => Report = await repair.PreviewAsync(ct);

    public async Task OnPostAsync(CancellationToken ct)
    {
        Applied = await repair.ApplyAsync(ct);
        Report = await repair.PreviewAsync(ct);
    }
}
