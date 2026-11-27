using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrecioHerramientas.Core.Scraping;
using PrecioHerramientas.Core.Text;
using PrecioHerramientas.Scrapers.Abstractions;
using PrecioHerramientas.Scrapers.Http;
using PrecioHerramientas.Scrapers.Parsing;
using PrecioHerramientas.Scrapers.Platforms;

namespace PrecioHerramientas.Scrapers.Stores;

/// <summary>
/// Construmart (Magento 2). Precio y stock dependen de la sucursal que el visitante elige en
/// el modal "Elige tu tienda" (región + tienda); la API GraphQL pública no entrega precios.
/// Por eso todo se hace en una sola sesión de Chromium reutilizada:
/// <list type="number">
/// <item>Se leen las sucursales de las regiones configuradas desde el propio selector del sitio
/// (o de <c>Branches:List</c> en la configuración).</item>
/// <item>Para cada sucursal: se elige en el modal (o con <c>Branches:CookieName</c> si se conoce la
/// cookie) y se corren todas las búsquedas. <c>/catalogsearch/result/?q=</c> suele redirigir a
/// una categoría; se sigue y se pagina con <c>?p=N</c>.</item>
/// <item>La grilla de Magento trae el precio en <c>data-price-amount</c> y el SKU en
/// <c>data-product-sku</c>. Cada producto se guarda con su sucursal.</item>
/// </list>
/// Al elegir la primera sucursal se registra en el log qué cookies cambió el sitio, para poder
/// pasar al modo por cookie (más liviano) una vez conocido el mecanismo.
/// </summary>
public sealed class ConstrumartScraper(IServiceProvider services, IOptions<ScraperOptions> options, ILogger<ConstrumartScraper> logger)
    : BrowserCaptureScraperBase(services, options, logger)
{
    public const string Key = "construmart";

    public override StoreDescriptor Store => Descriptor;

    private static readonly StoreDescriptor Descriptor = new(Key, "Construmart", "https://www.construmart.cl");

    protected override IReadOnlyList<string> DefaultQueries { get; } =
    [
        "taladro", "rotomartillo", "esmeril angular", "atornillador", "sierra circular", "lijadora", "compresor", "hidrolavadora",
    ];

    protected override string? BuildSearchPageUrl(string query, int page) =>
        page == 1 ? new Uri(BaseUri, $"/catalogsearch/result/?q={Uri.EscapeDataString(query)}").AbsoluteUri : null;

    protected override ListingSelectors? FallbackSelectors => Grid;

    /// <summary>Grilla estándar de Magento 2 (Luma y derivados).</summary>
    public static ListingSelectors Grid { get; } = new()
    {
        // Solo la grilla de resultados: la página tiene además un widget de recomendaciones
        // (".widget-product-grid") con productos de otras categorías.
        Card = "#amasty-shopby-product-list li.product-item, div.products.wrapper li.product-item",
        Title = "a.product-item-link, .product-item-name a, .product-name a",
        Link = "a.product-item-link, .product-item-name a, a.product-item-photo, a",
        Brand = ".product-brand, .brand, [class*='marca']",
        Price = "[data-price-type='finalPrice'][data-price-amount], [data-price-type='finalPrice'] .price, .special-price .price, .price-final_price .price",
        ListPrice = "[data-price-type='oldPrice'][data-price-amount], [data-price-type='oldPrice'] .price, .old-price .price",
        Image = "img.product-image-photo, img",
        Sku = "[data-product-sku], .price-box[data-product-id]",
        SkuAttribute = "data-product-sku",
        OutOfStock = ".stock.unavailable, .out-of-stock, .unavailable, .sin-stock",
        WaitFor = "#amasty-shopby-product-list, div.products.wrapper, .message.notice",
    };

    private static readonly Dictionary<string, string> DefaultModalSelectors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Modal "Elige tu tienda" (nov. 2026): #region-selector-popup, #store-selector-popup y el botón
        // "ENTRAR A CONSTRUMART.CL". Hay un par de selects iguales en el encabezado (#region-selector),
        // dentro de un panel cerrado: las alternativas se prueban en orden, así que va primero el modal.
        ["OpenModal"] = "#store-drawer-button, button:has-text('Selecciona tu tienda'), button:has-text('Elige tu tienda'), a:has-text('Elige tu tienda')",
        ["Region"] = "#region-selector-popup, select[id*='region' i], select[name*='region' i]",
        ["Store"] = "#store-selector-popup, select[id*='store' i], select[id*='tienda' i], select[name*='store' i], select[name*='tienda' i], select[name*='sucursal' i]",
        ["Confirm"] = "#enter-store-button, button:has-text('Entrar a'), button:has-text('Confirmar'), button:has-text('Aceptar'), button:has-text('Guardar')",
    };

    private string Modal(string key) =>
        Options.Branches.Selectors.TryGetValue(key, out var custom) && custom.Length > 0 ? custom : DefaultModalSelectors[key];

    // ---------- Plan: sucursal por sucursal ----------

    /// <summary>El sitio guarda la sucursal elegida enviando su id a esta URL (grupo de clientes de Magento).</summary>
    private const string DefaultSwitchUrl = "/catalogswitcher/switch/customergroup";
    private const string DefaultSwitchField = "customer_group_id";

    // Sucursal nula = precios generales del sitio (sin sucursal elegida).
    private readonly Dictionary<string, (BranchDefinition? Branch, string Query)> _work = new();
    private string? _currentBranchId;
    private bool _cookiesLogged;

    protected override async Task<IReadOnlyList<string>> PlanAsync(CancellationToken ct)
    {
        var branches = await BranchesAsync(ct);
        _work.Clear();

        if (branches.Count == 0)
        {
            // El sitio ya no deja elegir sucursal (o cambió el selector): mejor precios generales que nada.
            Logger.LogWarning("{Store}: no se encontraron sucursales para {Regions}; se usan los precios generales del sitio. " +
                "Revise Branches:Regions o Branches:Selectors si el selector de sucursal sigue existiendo.",
                Store.Key, string.Join(", ", Options.Branches.Regions));
            foreach (var query in Queries) _work[query] = (null, query);
            return _work.Keys.ToList();
        }

        Logger.LogInformation("{Store}: {Count} sucursales: {Branches}", Store.Key, branches.Count, string.Join(", ", branches.Select(b => $"{b.Name} ({b.Id})")));

        // Primero todas las búsquedas de una sucursal: se cambia de sucursal lo menos posible.
        foreach (var branch in branches)
        foreach (var query in Queries)
            _work[$"{branch.Name} · {query}"] = (branch, query);
        return _work.Keys.ToList();
    }

    private async Task<IReadOnlyList<BranchDefinition>> BranchesAsync(CancellationToken ct)
    {
        var regions = Options.Branches.Regions;
        if (Options.Branches.List.Count > 0)
            return Options.Branches.List
                .Where(b => regions.Count == 0 || regions.Any(r => Matches(b.Region, r)))
                .ToList();

        var session = await SessionAsync(ct);
        var home = await session.LoadAsync(BaseUri.AbsoluteUri, _ => false, null, ct);
        if (home is not null && IsBlocked(home.Html))
            throw new ScrapeFailedException("Construmart rechazó el acceso del navegador (403 de su filtro anti-bots). " +
                "No es un problema de sucursales: el sitio bloqueó la visita.");

        var regionOptions = RealOptions(await session.ReadOptionsAsync(Modal("Region"), ct));
        if (regionOptions.Count == 0)
        {
            // El modal aparece solo; si no está abierto, se abre desde el botón de la tienda.
            await session.ClickAsync(Modal("OpenModal"), ct);
            regionOptions = RealOptions(await session.ReadOptionsAsync(Modal("Region"), ct));
        }

        var found = new List<BranchDefinition>();
        foreach (var wanted in regions)
        {
            var region = regionOptions.FirstOrDefault(o => Matches(o.Label, wanted));
            if (region is null)
            {
                Logger.LogWarning("{Store}: la región \"{Region}\" no está en el selector ({Available})",
                    Store.Key, wanted, string.Join(", ", regionOptions.Select(o => o.Label)));
                continue;
            }
            if (!await session.ChooseOptionAsync(Modal("Region"), region.Value, ct))
            {
                Logger.LogWarning("{Store}: no se pudo elegir la región \"{Region}\" en el selector", Store.Key, region.Label);
                continue;
            }
            await session.WaitForIdleAsync(ct);   // las tiendas de la región se cargan por AJAX

            found.AddRange(RealOptions(await session.ReadOptionsAsync(Modal("Store"), ct))
                .Select(o => new BranchDefinition { Id = o.Value, Name = TitleCase(o.Label), Region = region.Label }));
        }
        return found.DistinctBy(b => b.Id).ToList();
    }

    private static bool IsBlocked(string html) =>
        html.Contains("<title>403 Forbidden</title>", StringComparison.OrdinalIgnoreCase)
        || html.Contains("Error 403 Forbidden", StringComparison.OrdinalIgnoreCase);

    // El selector trae los nombres en mayúsculas ("LAS CONDES"): se muestran como "Las Condes".
    private static string TitleCase(string name) =>
        System.Globalization.CultureInfo.GetCultureInfo("es-CL").TextInfo.ToTitleCase(name.Trim().ToLowerInvariant());

    // Quita la opción vacía o "Selecciona..." de un <select>.
    private static List<SelectOption> RealOptions(IReadOnlyList<SelectOption> options) =>
        options.Where(o => o.Value.Length > 0 && !TextNormalizer.ToSearchText(o.Label).StartsWith("selecc", StringComparison.Ordinal)).ToList();

    private static bool Matches(string? text, string wanted)
    {
        var a = TextNormalizer.ToSearchText(text);
        var b = TextNormalizer.ToSearchText(wanted);
        return a.Length > 0 && b.Length > 0 && (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal));
    }

    // ---------- Cada búsqueda en su sucursal ----------

    protected override async IAsyncEnumerable<ScrapedProduct> ScrapeQueryAsync(string item, QueryContext context, [EnumeratorCancellation] CancellationToken ct)
    {
        var (branch, query) = _work[item];
        if (branch is not null && _currentBranchId != branch.Id)
        {
            var error = await SelectBranchAsync(branch, ct);
            if (error is not null)
            {
                context.Failure = error;
                yield break;
            }
            _currentBranchId = branch.Id;
        }

        await PoliteDelayAsync(ct);
        await foreach (var product in base.ScrapeQueryAsync(query, context, ct))
            yield return branch is null ? product : product with { BranchCode = branch.Id, BranchName = branch.Name };
    }

    // La búsqueda redirige a una categoría: las páginas siguientes se piden sobre esa URL.
    protected override string? NextPageUrl(string query, string finalUrl, int page)
    {
        var uri = new UriBuilder(finalUrl);
        var parameters = System.Web.HttpUtility.ParseQueryString(uri.Query);
        parameters["p"] = page.ToString();
        uri.Query = parameters.ToString();
        return uri.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Fija la sucursal en la sesión, de lo más liviano a lo más pesado: cookie conocida
    /// (Branches:CookieName), el mismo POST que hace el sitio (Branches:SwitchUrl, por defecto
    /// /catalogswitcher/switch/customergroup) o, si eso falla, el modal. Devuelve el motivo si no se pudo.
    /// </summary>
    private async Task<string?> SelectBranchAsync(BranchDefinition branch, CancellationToken ct)
    {
        var session = await SessionAsync(ct);

        if (Options.Branches.CookieName is { Length: > 0 } cookie)
        {
            await session.SetCookieAsync(cookie, branch.Id, BaseUri.AbsoluteUri, ct);
            Logger.LogInformation("{Store}: sucursal {Branch} fijada con la cookie {Cookie}", Store.Key, branch.Name, cookie);
            return null;
        }

        var switchUrl = Options.Branches.SwitchUrl ?? DefaultSwitchUrl;
        if (switchUrl.Length > 0)
        {
            var field = Options.Branches.SwitchField is { Length: > 0 } custom ? custom : DefaultSwitchField;
            try
            {
                var result = await session.PostFormAsync(new Uri(BaseUri, switchUrl).AbsoluteUri, new Dictionary<string, string> { [field] = branch.Id }, ct);
                if (result.Status == 200 && !result.Body.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInformation("{Store}: sucursal {Branch} ({Id}) seleccionada", Store.Key, branch.Name, branch.Id);
                    return null;
                }
                Logger.LogWarning("{Store}: {Url} respondió {Status} para la sucursal {Branch}; se usa el modal", Store.Key, switchUrl, result.Status, branch.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning("{Store}: no se pudo fijar la sucursal {Branch} con {Url} ({Message}); se usa el modal", Store.Key, branch.Name, switchUrl, ex.Message);
            }
        }

        return await SelectBranchWithModalAsync(session, branch, ct);
    }

    private async Task<string?> SelectBranchWithModalAsync(IBrowserSession session, BranchDefinition branch, CancellationToken ct)
    {
        var before = _cookiesLogged ? null : await session.GetCookiesAsync(ct);
        await session.LoadAsync(BaseUri.AbsoluteUri, _ => false, null, ct);

        var regionOptions = RealOptions(await session.ReadOptionsAsync(Modal("Region"), ct));
        if (regionOptions.Count == 0)
        {
            await session.ClickAsync(Modal("OpenModal"), ct);
            regionOptions = RealOptions(await session.ReadOptionsAsync(Modal("Region"), ct));
        }
        var region = regionOptions.FirstOrDefault(o => Matches(o.Label, branch.Region));
        if (region is null) return $"la región \"{branch.Region}\" no está en el selector de sucursal";
        if (!await session.ChooseOptionAsync(Modal("Region"), region.Value, ct))
        {
            // Con una sucursal ya elegida el modal no vuelve a aparecer: se abre el panel de tiendas.
            await session.ClickAsync(Modal("OpenModal"), ct);
            await session.WaitForIdleAsync(ct);
            if (!await session.ChooseOptionAsync(Modal("Region"), region.Value, ct))
                return $"no se pudo elegir la región \"{branch.Region}\" en el selector de sucursal";
        }
        await session.WaitForIdleAsync(ct);

        if (!await session.ChooseOptionAsync(Modal("Store"), branch.Id, ct))
            return $"no se pudo elegir la sucursal \"{branch.Name}\" ({branch.Id})";
        await session.ClickAsync(Modal("Confirm"), ct);   // algunos selectores guardan al cambiar, sin botón
        await session.WaitForIdleAsync(ct);

        if (before is not null)
        {
            var after = await session.GetCookiesAsync(ct);
            var changed = after.Where(c => !before.TryGetValue(c.Key, out var old) || old != c.Value).Select(c => c.Key).ToList();
            Logger.LogInformation("{Store}: al elegir la sucursal {Branch} con el modal cambiaron las cookies: {Cookies}",
                Store.Key, branch.Name, changed.Count > 0 ? string.Join(", ", changed) : "(ninguna)");
            _cookiesLogged = true;
        }
        Logger.LogInformation("{Store}: sucursal {Branch} ({Id}) seleccionada con el modal", Store.Key, branch.Name, branch.Id);
        return null;
    }
}
