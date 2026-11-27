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
                captured.Enqueue(new CapturedJson(response.Url, HttpText.Decode(body, Charset(response.Headers))));
        }
        catch (PlaywrightException)
        {
            // La página navegó y descartó la respuesta antes de leerla.
        }
    }

    private static string? Charset(IReadOnlyDictionary<string, string> headers) =>
        headers.TryGetValue("content-type", out var type)
        && System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(type, out var media) ? media.CharSet : null;

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
        return HttpText.Decode(await response.BodyAsync(), Charset(response.Headers));
    }

    private async Task<IPage> PageAsync() => _page ??= await PlaywrightPageFetcher.NewPageAsync(context);

    // Con una lista "a, b, c" Playwright toma la primera coincidencia del documento, no la primera
    // alternativa. Aquí se prueba cada alternativa en orden: p. ej. el select del modal antes que
    // uno igual del encabezado que está dentro de un panel cerrado y no se puede usar.

    public async Task<IReadOnlyList<SelectOption>> ReadOptionsAsync(string selectSelector, CancellationToken ct)
    {
        var page = await PageAsync();
        ILocator? fallback = null;
        foreach (var alternative in SelectorList.Split(selectSelector))
        {
            var select = page.Locator(alternative).First;
            try
            {
                await select.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = fallback is null ? 10_000 : 2_000 });
            }
            catch (TimeoutException)
            {
                continue;
            }
            if (await select.IsVisibleAsync()) return await OptionsOf(select);
            fallback ??= select;
        }
        return fallback is null ? [] : await OptionsOf(fallback);
    }

    private static async Task<IReadOnlyList<SelectOption>> OptionsOf(ILocator select)
    {
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
        var page = await PageAsync();
        foreach (var alternative in SelectorList.Split(selectSelector))
        {
            try
            {
                await page.Locator(alternative).First.SelectOptionAsync(value, new LocatorSelectOptionOptions { Timeout = 5_000 });
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
            {
                // No está, está oculto o no tiene esa opción: se prueba la siguiente alternativa.
            }
        }
        return false;
    }

    public async Task<bool> ClickAsync(string selector, CancellationToken ct)
    {
        var page = await PageAsync();
        foreach (var alternative in SelectorList.Split(selector))
        {
            try
            {
                await page.Locator(alternative).Filter(new LocatorFilterOptions { Visible = true }).First
                    .ClickAsync(new LocatorClickOptions { Timeout = 3_000 });
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
            {
                // Se prueba la siguiente alternativa.
            }
        }
        return false;
    }

    public async Task<FormPostResult> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, CancellationToken ct)
    {
        // fetch desde la página: mismas cookies, origen y encabezados que usa el propio sitio.
        var result = await (await PageAsync()).EvaluateAsync<System.Text.Json.JsonElement>("""
            async ([url, fields]) => {
                const response = await fetch(url, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', 'X-Requested-With': 'XMLHttpRequest' },
                    body: new URLSearchParams(fields).toString(),
                });
                return { status: response.status, body: (await response.text()).slice(0, 2000) };
            }
            """, new object[] { url, form });
        ct.ThrowIfCancellationRequested();
        // Playwright devuelve el objeto de JavaScript como JSON; se lee a mano.
        return new FormPostResult(result.GetProperty("status").GetInt32(), result.GetProperty("body").GetString() ?? "");
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
