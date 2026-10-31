using System.Net;
using System.Text;

namespace PrecioHerramientas.Tests.Support;

/// <summary>Responde solicitudes HTTP con contenido fijo, sin salir a internet.</summary>
public sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Ok(string content, string mediaType = "application/json") =>
        new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, mediaType) };
}

public sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

public static class Fixture
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}

/// <summary>Devuelve el HTML que indique la función, en lugar de descargarlo.</summary>
public sealed class FakePageFetcher(Func<string, string?> respond) : PrecioHerramientas.Scrapers.Http.IPageFetcher
{
    public Task<string?> GetHtmlAsync(string url, string? waitForSelector, CancellationToken ct) => Task.FromResult(respond(url));
}

/// <summary>Sesión de navegador simulada: cada URL devuelve lo que indiquen las funciones.</summary>
public sealed class FakeBrowserSession(
    Func<string, PrecioHerramientas.Scrapers.Http.BrowserPage?> load,
    Func<string, string?>? get = null) : PrecioHerramientas.Scrapers.Http.IBrowserSession, PrecioHerramientas.Scrapers.Http.IBrowserSessionFactory
{
    public List<string> Loaded { get; } = new();
    public List<string> Fetched { get; } = new();
    public int Opened { get; private set; }
    public int Disposed { get; private set; }

    public Task<PrecioHerramientas.Scrapers.Http.BrowserPage?> LoadAsync(string url, Func<string, bool> captureJson, string? waitForSelector, CancellationToken ct)
    {
        Loaded.Add(url);
        Actions.Add($"load:{url}");
        var page = load(url);
        // Igual que el navegador real: solo quedan las respuestas que pasan el filtro.
        return Task.FromResult(page is null ? null : page with { Responses = page.Responses.Where(r => captureJson(r.Url)).ToList() });
    }

    public Task<string?> GetAsync(string url, CancellationToken ct)
    {
        Fetched.Add(url);
        return Task.FromResult(get?.Invoke(url));
    }

    public Task<PrecioHerramientas.Scrapers.Http.IBrowserSession> OpenAsync(CancellationToken ct)
    {
        Opened++;
        return Task.FromResult<PrecioHerramientas.Scrapers.Http.IBrowserSession>(this);
    }

    public ValueTask DisposeAsync()
    {
        Disposed++;
        return ValueTask.CompletedTask;
    }

    // ---------- Interacción con la página (modal de sucursal) ----------

    /// <summary>Opciones de cada select según su selector; recibe lo elegido hasta ahora.</summary>
    public Func<string, IReadOnlyDictionary<string, string>, IReadOnlyList<PrecioHerramientas.Scrapers.Http.SelectOption>>? Options { get; set; }

    /// <summary>Último valor elegido en cada select (por selector).</summary>
    public Dictionary<string, string> Chosen { get; } = new();

    /// <summary>Acciones en orden: "load:url", "click:selector", "choose:selector=valor", "cookie:nombre=valor".</summary>
    public List<string> Actions { get; } = new();

    public Dictionary<string, string> Cookies { get; } = new();

    public Task<IReadOnlyList<PrecioHerramientas.Scrapers.Http.SelectOption>> ReadOptionsAsync(string selectSelector, CancellationToken ct) =>
        Task.FromResult(Options?.Invoke(selectSelector, Chosen) ?? []);

    public Task<bool> ChooseOptionAsync(string selectSelector, string value, CancellationToken ct)
    {
        var available = Options?.Invoke(selectSelector, Chosen) ?? [];
        if (!available.Any(o => o.Value == value)) return Task.FromResult(false);
        Chosen[selectSelector] = value;
        Actions.Add($"choose:{selectSelector}={value}");
        return Task.FromResult(true);
    }

    public Task<bool> ClickAsync(string selector, CancellationToken ct)
    {
        Actions.Add($"click:{selector}");
        return Task.FromResult(true);
    }

    public Task WaitForIdleAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyDictionary<string, string>> GetCookiesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Cookies));

    public Task SetCookieAsync(string name, string value, string url, CancellationToken ct)
    {
        Cookies[name] = value;
        Actions.Add($"cookie:{name}={value}");
        return Task.CompletedTask;
    }
}
