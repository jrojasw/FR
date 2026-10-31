using System.Text.Json;
using System.Text.RegularExpressions;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>
/// Utilidades para la búsqueda guiada (Endeca "assembler") de Oracle Commerce Cloud:
/// <c>/ccstore/v1/assembler/pages/Default/services/guidedsearch</c>.
/// </summary>
public static partial class OccGuidedSearch
{
    /// <summary>
    /// Estado de navegación ("N-1033696118") al que el buscador redirige un término. Se busca
    /// solo bajo claves de redirección ("endeca:redirect", "redirect", "redirectUrl"...): las
    /// facetas del mismo JSON también traen estados N-..., y no son el destino.
    /// </summary>
    public static string? RedirectNavigationState(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return FindRedirect(doc.RootElement, insideRedirect: false, depth: 0);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Total de registros (<c>totalNumRecs</c>) en cualquier nivel de la respuesta.</summary>
    public static int? TotalRecords(string json)
    {
        var match = TotalNumRecs().Match(json);
        return match.Success && int.TryParse(match.Groups["n"].Value, out var total) ? total : null;
    }

    private static string? FindRedirect(JsonElement element, bool insideRedirect, int depth)
    {
        if (depth > 24) return null;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var inside = insideRedirect || property.Name.Contains("redirect", StringComparison.OrdinalIgnoreCase);
                    if (FindRedirect(property.Value, inside, depth + 1) is { } found) return found;
                }
                return null;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (FindRedirect(item, insideRedirect, depth + 1) is { } found) return found;
                }
                return null;
            case JsonValueKind.String when insideRedirect:
                var match = NavigationState().Match(element.GetString()!);
                return match.Success ? match.Value : null;
            default:
                return null;
        }
    }

    [GeneratedRegex(@"N-[0-9A-Za-z]+(?:Z[0-9A-Za-z]+)*")]
    private static partial Regex NavigationState();

    [GeneratedRegex(@"""totalNumRecs""\s*:\s*(?<n>\d+)")]
    private static partial Regex TotalNumRecs();
}
