using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PrecioHerramientas.Scrapers.Abstractions;

namespace PrecioHerramientas.Scrapers.Http;

/// <summary>
/// Carga páginas en Chromium headless para tiendas que pintan los precios con
/// JavaScript. El navegador se inicia la primera vez que se usa y se comparte
/// entre scrapers. Requiere instalar los navegadores de Playwright
/// (ver README: <c>INSTALL_PLAYWRIGHT=true</c> en Docker).
/// </summary>
public sealed class PlaywrightPageFetcher(IOptions<ScraperOptions> options, ILogger<PlaywrightPageFetcher> logger)
    : IPageFetcher, IBrowserSessionFactory, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<string?> GetHtmlAsync(string url, string? waitForSelector, CancellationToken ct)
    {
        await using var context = await NewContextAsync();
        var page = await NewPageAsync(context);

        var timeout = options.Value.RequestTimeoutSeconds * 1000f;
        var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeout });
        if (response is { Status: 404 }) return null;

        if (!string.IsNullOrEmpty(waitForSelector))
        {
            try
            {
                await page.WaitForSelectorAsync(waitForSelector, new PageWaitForSelectorOptions { Timeout = timeout });
            }
            catch (TimeoutException)
            {
                logger.LogWarning("No apareció {Selector} en {Url}; se procesa el HTML disponible", waitForSelector, url);
            }
        }

        ct.ThrowIfCancellationRequested();
        return await page.ContentAsync();
    }

    public async Task<IBrowserSession> OpenAsync(CancellationToken ct) =>
        new PlaywrightSession(await NewContextAsync(), options.Value.RequestTimeoutSeconds * 1000f, logger);

    private async Task<IBrowserContext> NewContextAsync()
    {
        var browser = await GetBrowserAsync();
        return await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = options.Value.BrowserUserAgent is { Length: > 0 } ua ? ua : DesktopChromeUserAgent(browser.Version),
            Locale = "es-CL",
            TimezoneId = "America/Santiago",
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept-Language"] = "es-CL,es;q=0.9,en;q=0.6",
            },
        });
    }

    internal static async Task<IPage> NewPageAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        // Imágenes, fuentes y video no hacen falta para leer precios.
        await page.RouteAsync("**/*", route =>
            route.Request.ResourceType is "image" or "media" or "font" ? route.AbortAsync() : route.ContinueAsync());
        return page;
    }

    /// <summary>
    /// El User-Agent por defecto de Chromium headless dice "HeadlessChrome", y varias
    /// tiendas lo rechazan. Se usa el de Chrome de escritorio con la misma versión.
    /// </summary>
    public static string DesktopChromeUserAgent(string browserVersion)
    {
        var major = browserVersion.Split('.')[0];
        return $"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{major}.0.0.0 Safari/537.36";
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is { IsConnected: true }) return _browser;
        await _initLock.WaitAsync();
        try
        {
            if (_browser is { IsConnected: true }) return _browser;
            _playwright ??= await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-dev-shm-usage"],
            });
            return _browser;
        }
        catch (Exception ex) when (ex is PlaywrightException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                "No se pudo iniciar Chromium para Playwright. Revise que la imagen se construyó con " +
                "INSTALL_PLAYWRIGHT=true y que .playwright/node/*/node tiene permiso de ejecución. " + ex.Message, ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
        _initLock.Dispose();
    }
}
