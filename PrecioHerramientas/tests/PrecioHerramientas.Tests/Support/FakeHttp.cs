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
