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
        Card = "li.product-item, div.product-item, .products-grid .item",
        Title = "a.product-item-link, .product-item-name a, .product-name a",
        Link = "a.product-item-link, .product-item-name a, a.product-item-photo, a",
        Brand = ".product-brand, .brand, [class*='marca']",
        Price = "[data-price-type='finalPrice'][data-price-amount], [data-price-type='finalPrice'] .price, .special-price .price, .price-final_price .price",
        ListPrice = "[data-price-type='oldPrice'][data-price-amount], [data-price-type='oldPrice'] .price, .old-price .price",
        Image = "img.product-image-photo, img",
        Sku = "[data-product-sku], .price-box[data-product-id]",
        SkuAttribute = "data-product-sku",
        OutOfStock = ".stock.unavailable, .out-of-stock, .unavailable, .sin-stock",
        WaitFor = "li.product-item, div.product-item, .message.notice",
    };

    private static readonly Dictionary<string, string> DefaultModalSelectors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OpenModal"] = "[data-role='store-selector'], .store-selector, .js-store-selector, button:has-text('Elige tu tienda'), a:has-text('Elige tu tienda'), button:has-text('Cambiar tienda'), a:has-text('Cambiar tienda')",
        ["Region"] = "select[name*='region' i], select[id*='region' i]",
        ["Store"] = "select[name*='tienda' i], select[id*='tienda' i], select[name*='store' i], select[id*='store' i], select[name*='sucursal' i]",
        ["Confirm"] = "button:has-text('Confirmar'), button:has-text('Aceptar'), button:has-text('Guardar'), button:has-text('Seleccionar'), button:has-text('Continuar')",
    };

    private string Modal(string key) =>
        Options.Branches.Selectors.TryGetValue(key, out var custom) && custom.Length > 0 ? custom : DefaultModalSelectors[key];

    // ---------- Plan: sucursal por sucursal ----------

    private readonly Dictionary<string, (BranchDefinition Branch, string Query)> _work = new();
    private string? _currentBranchId;
    private bool _cookiesLogged;

    protected override async Task<IReadOnlyList<string>> PlanAsync(CancellationToken ct)
    {
        var branches = await BranchesAsync(ct);
        if (branches.Count == 0)
            throw new ScrapeFailedException(
                $"Construmart: no se encontraron sucursales para las regiones {string.Join(", ", Options.Branches.Regions)}. " +
                "Revise Branches:Regions o los selectores del modal (Branches:Selectors).");

        Logger.LogInformation("{Store}: {Count} sucursales: {Branches}", Store.Key, branches.Count, string.Join(", ", branches.Select(b => $"{b.Name} ({b.Id})")));

        // Primero todas las búsquedas de una sucursal: se cambia de sucursal lo menos posible.
        _work.Clear();
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
        await session.LoadAsync(BaseUri.AbsoluteUri, _ => false, null, ct);
        await session.ClickAsync(Modal("OpenModal"), ct);   // si el modal ya está abierto, no hay botón: está bien

        var regionOptions = RealOptions(await session.ReadOptionsAsync(Modal("Region"), ct));
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
            await session.ChooseOptionAsync(Modal("Region"), region.Value, ct);
            await session.WaitForIdleAsync(ct);   // las tiendas de la región suelen cargarse por AJAX

            found.AddRange(RealOptions(await session.ReadOptionsAsync(Modal("Store"), ct))
                .Select(o => new BranchDefinition { Id = o.Value, Name = o.Label, Region = region.Label }));
        }
        return found.DistinctBy(b => b.Id).ToList();
    }

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
        if (_currentBranchId != branch.Id)
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
            yield return product with { BranchCode = branch.Id, BranchName = branch.Name };
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

    /// <summary>Fija la sucursal en la sesión. Devuelve el motivo si no se pudo.</summary>
    private async Task<string?> SelectBranchAsync(BranchDefinition branch, CancellationToken ct)
    {
        var session = await SessionAsync(ct);

        if (Options.Branches.CookieName is { Length: > 0 } cookie)
        {
            await session.SetCookieAsync(cookie, branch.Id, BaseUri.AbsoluteUri, ct);
            Logger.LogInformation("{Store}: sucursal {Branch} fijada con la cookie {Cookie}", Store.Key, branch.Name, cookie);
            return null;
        }

        var before = _cookiesLogged ? null : await session.GetCookiesAsync(ct);
        await session.LoadAsync(BaseUri.AbsoluteUri, _ => false, null, ct);
        await session.ClickAsync(Modal("OpenModal"), ct);

        var regionOptions = RealOptions(await session.ReadOptionsAsync(Modal("Region"), ct));
        var region = regionOptions.FirstOrDefault(o => Matches(o.Label, branch.Region));
        if (region is null || !await session.ChooseOptionAsync(Modal("Region"), region.Value, ct))
            return $"no se pudo elegir la región \"{branch.Region}\" en el selector de sucursal";
        await session.WaitForIdleAsync(ct);

        if (!await session.ChooseOptionAsync(Modal("Store"), branch.Id, ct))
            return $"no se pudo elegir la sucursal \"{branch.Name}\" ({branch.Id})";
        await session.ClickAsync(Modal("Confirm"), ct);   // algunos selectores guardan al cambiar, sin botón
        await session.WaitForIdleAsync(ct);

        if (before is not null)
        {
            var after = await session.GetCookiesAsync(ct);
            var changed = after.Where(c => !before.TryGetValue(c.Key, out var old) || old != c.Value).Select(c => c.Key).ToList();
            Logger.LogInformation("{Store}: al elegir la sucursal {Branch} cambiaron las cookies: {Cookies}. " +
                "Si una de ellas guarda el id de la sucursal, configure Branches:CookieName para evitar el modal.",
                Store.Key, branch.Name, changed.Count > 0 ? string.Join(", ", changed) : "(ninguna)");
            _cookiesLogged = true;
        }
        Logger.LogInformation("{Store}: sucursal {Branch} ({Id}) seleccionada", Store.Key, branch.Name, branch.Id);
        return null;
    }
}
