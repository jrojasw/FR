using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace PrecioHerramientas.Scrapers.Http;

/// <summary>Sesión de Playwright: un contexto (cookies propias) con una página reutilizada.</summary>
internal sealed class PlaywrightSession(IBrowserContext context, float timeoutMs, ILogger logger) : IBrowserSession
{
    /// <summary>Respuestas más grandes que esto no son listados de productos (y llenarían la memoria).</summary>
    private const int MaxCapturedBytes = 8 * 1024 * 1024;

    private IPage? _page;

    public async Task<BrowserPage?> LoadAsync(string url, Func<string, bool> captureJson, string? waitForSelector, CancellationToken ct)
    {
        var page = await PageAsync();

        var captured = new ConcurrentQueue<CapturedJson>();
        var pending = new ConcurrentBag<Task>();
        void OnResponse(object? _, IResponse response)
        {
            if (response.Request.ResourceType is not ("xhr" or "fetch") || !captureJson(response.Url) || response.Status != 200) return;
            if (!(response.Headers.TryGetValue("content-type", out var type) && type.Contains("json", StringComparison.OrdinalIgnoreCase))) return;
            pending.Add(ReadAsync(response, captured));
        }

        page.Response += OnResponse;
        try
        {
            var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeoutMs });
            if (response is { Status: 404 }) return null;

            await WaitUntilSettledAsync(page, url);

            if (!string.IsNullOrEmpty(waitForSelector))
            {
                try
                {
                    await page.WaitForSelectorAsync(waitForSelector, new PageWaitForSelectorOptions { Timeout = Math.Min(timeoutMs, 10_000) });
                }
                catch (TimeoutException)
                {
                    logger.LogDebug("No apareció {Selector} en {Url}", waitForSelector, page.Url);
                }
            }

            await Task.WhenAll(pending);
            ct.ThrowIfCancellationRequested();
            return new BrowserPage(page.Url, await page.ContentAsync(), captured.ToList());
        }
        finally
        {
            page.Response -= OnResponse;
        }
    }

    /// <summary>
    /// Espera a que no haya tráfico de red y a que la URL deje de cambiar: algunos sitios
    /// redirigen con JavaScript (p. ej. la búsqueda "taladro" lleva a una categoría).
    /// </summary>
    private async Task WaitUntilSettledAsync(IPage page, string requestedUrl)
    {
        string? lastUrl = null;
        for (int i = 0; i < 4 && page.Url != lastUrl; i++)
        {
            lastUrl = page.Url;
            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15_000 });
            }
            catch (TimeoutException)
            {
                // Sitios con conexiones permanentes (analítica, chat) nunca quedan "idle".
            }
            await page.WaitForTimeoutAsync(1_500);
        }
        if (page.Url != requestedUrl)
            logger.LogInformation("El sitio llevó {Requested} a {Final}", requestedUrl, page.Url);
    }

    private static async Task ReadAsync(IResponse response, ConcurrentQueue<CapturedJson> captured)
    {
        try
        {
            var body = await response.BodyAsync();
            if (body.Length <= MaxCapturedBytes)
                captured.Enqueue(new CapturedJson(response.Url, System.Text.Encoding.UTF8.GetString(body)));
        }
        catch (PlaywrightException)
        {
            // La página navegó y descartó la respuesta antes de leerla.
        }
    }

    public async Task<string?> GetAsync(string url, CancellationToken ct)
    {
        // APIRequest del contexto comparte las cookies de la sesión del navegador.
        var response = await context.APIRequest.GetAsync(url, new APIRequestContextOptions
        {
            Timeout = timeoutMs,
            Headers = new Dictionary<string, string> { ["Accept"] = "application/json, text/plain, */*" },
        });
        if (response.Status == 404) return null;
        if (!response.Ok)
            throw new HttpRequestException($"HTTP {response.Status} en {url}", null, (System.Net.HttpStatusCode)response.Status);
        ct.ThrowIfCancellationRequested();
        return await response.TextAsync();
    }

    private async Task<IPage> PageAsync() => _page ??= await PlaywrightPageFetcher.NewPageAsync(context);

    public async Task<IReadOnlyList<SelectOption>> ReadOptionsAsync(string selectSelector, CancellationToken ct)
    {
        var select = (await PageAsync()).Locator(selectSelector).First;
        try
        {
            await select.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 10_000 });
        }
        catch (TimeoutException)
        {
            return [];
        }
        var options = new List<SelectOption>();
        foreach (var option in await select.Locator("option").AllAsync())
        {
            var value = await option.GetAttributeAsync("value") ?? "";
            var label = (await option.InnerTextAsync()).Trim();
            options.Add(new SelectOption(value.Trim(), label));
        }
        return options;
    }

    public async Task<bool> ChooseOptionAsync(string selectSelector, string value, CancellationToken ct)
    {
        try
        {
            await (await PageAsync()).Locator(selectSelector).First.SelectOptionAsync(value, new LocatorSelectOptionOptions { Timeout = 10_000 });
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
        {
            return false;
        }
    }

    public async Task<bool> ClickAsync(string selector, CancellationToken ct)
    {
        var target = (await PageAsync()).Locator(selector).Filter(new LocatorFilterOptions { Visible = true }).First;
        try
        {
            await target.ClickAsync(new LocatorClickOptions { Timeout = 5_000 });
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
        {
            return false;
        }
    }

    public async Task WaitForIdleAsync(CancellationToken ct)
    {
        var page = await PageAsync();
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15_000 });
        }
        catch (TimeoutException)
        {
            // Sitios con conexiones permanentes nunca quedan "idle".
        }
        await page.WaitForTimeoutAsync(1_000);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetCookiesAsync(CancellationToken ct) =>
        (await context.CookiesAsync()).GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.Last().Value);

    public Task SetCookieAsync(string name, string value, string url, CancellationToken ct) =>
        context.AddCookiesAsync([new Cookie { Name = name, Value = value, Url = url }]);

    public async ValueTask DisposeAsync() => await context.CloseAsync();
}
