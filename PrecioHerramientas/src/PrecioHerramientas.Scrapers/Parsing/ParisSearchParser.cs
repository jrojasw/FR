using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PrecioHerramientas.Core.Scraping;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>
/// Búsqueda de Paris.cl (Next.js con App Router). El HTML del servidor no trae
/// <c>__NEXT_DATA__</c> ni el JSON-LD como &lt;script&gt;: el JSON-LD de la página
/// (WebPage → mainEntity: ItemList con 30 productos, precio, SKU, marca e imagen) viaja
/// dentro de los datos de React, en llamadas <c>self.__next_f.push([1,"..."])</c>.
/// Se juntan esos textos, se ubica el objeto ItemList y se lee con <see cref="JsonLdProductExtractor"/>.
/// </summary>
public static partial class ParisSearchParser
{
    public sealed record Result(IReadOnlyList<ScrapedProduct> Products, int? Total);

    [GeneratedRegex("""self\.__next_f\.push\(\[1,("(?:[^"\\]|\\.)*")\]\)""", RegexOptions.CultureInvariant)]
    private static partial Regex FlightChunk();

    private const string ItemListMarker = "\"@type\":\"ItemList\"";

    public static Result Parse(string html, Uri baseUri)
    {
        if (ExtractItemListJson(html) is not { } json) return new([], null);
        return new(JsonLdProductExtractor.ExtractFromJson(json, baseUri), ReadTotal(json));
    }

    /// <summary>Texto JSON del objeto ItemList, o null si la página no lo trae.</summary>
    public static string? ExtractItemListJson(string html)
    {
        var flight = new StringBuilder();
        foreach (Match match in FlightChunk().Matches(html))
        {
            try
            {
                flight.Append(JsonSerializer.Deserialize<string>(match.Groups[1].Value));
            }
            catch (JsonException)
            {
                // Fragmento con un escape que no es JSON: se omite.
            }
        }

        // Si algún día lo publican como <script> normal, también sirve el HTML tal cual.
        foreach (var text in new[] { flight.ToString(), html })
        {
            var marker = text.IndexOf(ItemListMarker, StringComparison.Ordinal);
            if (marker < 0) continue;
            var start = text.LastIndexOf('{', marker);
            if (start >= 0 && MatchingBrace(text, start) is { } end)
                return text[start..(end + 1)];
        }
        return null;
    }

    private static int? ReadTotal(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("numberOfItems", out var total) && total.TryGetInt32(out var value) ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Posición de la llave que cierra la abierta en <paramref name="start"/>, ignorando las que van dentro de textos.</summary>
    private static int? MatchingBrace(string text, int start)
    {
        int depth = 0;
        bool inString = false;
        for (int i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }
            switch (c)
            {
                case '"': inString = true; break;
                case '{': depth++; break;
                case '}':
                    if (--depth == 0) return i;
                    break;
            }
        }
        return null;
    }
}
