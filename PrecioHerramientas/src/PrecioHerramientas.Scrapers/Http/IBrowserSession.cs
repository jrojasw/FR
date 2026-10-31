namespace PrecioHerramientas.Scrapers.Http;

/// <summary>Respuesta JSON que la página pidió mientras cargaba (XHR/fetch).</summary>
public sealed record CapturedJson(string Url, string Body);

/// <summary>Resultado de abrir una página en el navegador.</summary>
/// <param name="FinalUrl">URL después de las redirecciones del sitio (p. ej. búsqueda → categoría).</param>
/// <param name="Html">HTML ya renderizado por el JavaScript de la página.</param>
/// <param name="Responses">Respuestas JSON capturadas que pasaron el filtro.</param>
public sealed record BrowserPage(string FinalUrl, string Html, IReadOnlyList<CapturedJson> Responses);

/// <summary>
/// Una sesión de navegador (contexto de Playwright) que conserva cookies entre páginas,
/// como un visitante real. Se usa una por ejecución de scraper.
/// </summary>
public interface IBrowserSession : IAsyncDisposable
{
    /// <summary>
    /// Abre <paramref name="url"/>, espera a que el sitio termine de navegar (también
    /// redirecciones hechas con JavaScript) y devuelve el HTML y las respuestas JSON
    /// cuya URL cumple <paramref name="captureJson"/>. Null si la página no existe (404).
    /// </summary>
    Task<BrowserPage?> LoadAsync(string url, Func<string, bool> captureJson, string? waitForSelector, CancellationToken ct);

    /// <summary>GET con las cookies y encabezados de la sesión (para paginar una API que la página ya usó).</summary>
    Task<string?> GetAsync(string url, CancellationToken ct);

    // Interacción con la página abierta (p. ej. el selector de sucursal de una tienda).
    // Los selectores aceptan listas "a, b" y las pseudo-clases de Playwright (:has-text()).

    /// <summary>Opciones del primer &lt;select&gt; que coincide; vacío si no aparece en unos segundos.</summary>
    Task<IReadOnlyList<SelectOption>> ReadOptionsAsync(string selectSelector, CancellationToken ct);

    /// <summary>Elige una opción por su value (dispara "change", como un usuario). False si no está el select.</summary>
    Task<bool> ChooseOptionAsync(string selectSelector, string value, CancellationToken ct);

    /// <summary>Hace clic en el primer elemento visible que coincide. False si no aparece.</summary>
    Task<bool> ClickAsync(string selector, CancellationToken ct);

    /// <summary>Espera a que la página deje de tener tráfico de red.</summary>
    Task WaitForIdleAsync(CancellationToken ct);

    /// <summary>Cookies de la sesión: nombre → valor.</summary>
    Task<IReadOnlyDictionary<string, string>> GetCookiesAsync(CancellationToken ct);

    Task SetCookieAsync(string name, string value, string url, CancellationToken ct);
}

/// <summary>Opción de un &lt;select&gt;.</summary>
public sealed record SelectOption(string Value, string Label);

public interface IBrowserSessionFactory
{
    Task<IBrowserSession> OpenAsync(CancellationToken ct);
}
