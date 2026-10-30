namespace PrecioHerramientas.Scrapers.Abstractions;

public sealed class ScraperOptions
{
    public const string SectionName = "Scrapers";

    /// <summary>User-Agent con el que se identifican las solicitudes.</summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (compatible; PrecioHerramientasBot/1.0; +https://example.cl/bot)";

    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Configuración por tienda, indexada por la clave del scraper.</summary>
    public Dictionary<string, StoreScraperOptions> Stores { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public StoreScraperOptions For(string storeKey) =>
        Stores.TryGetValue(storeKey, out var options) ? options : new StoreScraperOptions();
}

public sealed class StoreScraperOptions
{
    public bool Enabled { get; set; }

    /// <summary>Expresión cron (Hangfire) para la ejecución programada. Por defecto, cada 6 horas.</summary>
    public string Cron { get; set; } = "0 */6 * * *";

    /// <summary>Pausa entre solicitudes a la tienda, para no sobrecargarla.</summary>
    public int DelayBetweenRequestsMs { get; set; } = 1500;

    public int MaxPagesPerQuery { get; set; } = 20;

    /// <summary>
    /// Búsquedas o rutas de categoría que se recorren. Su significado depende de la
    /// plataforma: término de búsqueda en VTEX, URL o ruta relativa en HTML.
    /// Si está vacío, se usan los valores por defecto del scraper.
    /// </summary>
    public List<string> Queries { get; set; } = new();

    /// <summary>Sobrescribe selectores CSS sin recompilar (ver cada scraper HTML).</summary>
    public Dictionary<string, string> Selectors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Scrapers HTML: true = siempre navegador (Playwright), false = solo HTTP,
    /// vacío = lo que defina cada tienda.
    /// </summary>
    public bool? UsePlaywright { get; set; }

    /// <summary>
    /// Scrapers HTML: URL de búsqueda relativa a la tienda con <c>{query}</c> y <c>{page}</c>,
    /// para ajustarla sin recompilar si la tienda cambia su sitio.
    /// </summary>
    public string? SearchUrlTemplate { get; set; }
}
