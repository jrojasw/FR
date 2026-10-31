using System.Text;
using System.Text.Json.Nodes;
using System.Web;

namespace PrecioHerramientas.Scrapers.Parsing;

/// <summary>
/// A partir de la URL de una API de listado que la página ya llamó, arma la de otra
/// página cambiando solo los parámetros de paginación que reconoce:
/// <c>_from/_to</c> (catálogo VTEX), <c>from/to</c> y <c>page</c> (Intelligent Search),
/// <c>extensions.variables</c> en base64 con from/to (GraphQL de VTEX IO),
/// <c>No/Nrpp</c> (búsqueda guiada de Oracle Commerce) y <c>offset/limit</c>.
/// </summary>
public static class PagingUrl
{
    /// <param name="page">Número de página, desde 1.</param>
    /// <returns>Null si la URL no tiene parámetros de paginación conocidos.</returns>
    public static string? ForPage(string apiUrl, int page)
    {
        var uri = new Uri(apiUrl);
        var query = HttpUtility.ParseQueryString(uri.Query);
        int index = page - 1;

        if (TryRange(query, "_from", "_to", index) || TryRange(query, "from", "to", index)
            || TryOffset(query, "No", "Nrpp", index) || TryOffset(query, "offset", "limit", index)
            || TryGraphQlVariables(query, index))
            return Rebuild(uri, query);

        if (query["page"] is { } current && int.TryParse(current, out _))
        {
            query["page"] = page.ToString();
            return Rebuild(uri, query);
        }
        return null;
    }

    private static bool TryRange(System.Collections.Specialized.NameValueCollection q, string fromKey, string toKey, int index)
    {
        if (!int.TryParse(q[fromKey], out var from) || !int.TryParse(q[toKey], out var to) || to < from) return false;
        var size = to - from + 1;
        q[fromKey] = (index * size).ToString();
        q[toKey] = (index * size + size - 1).ToString();
        return true;
    }

    private static bool TryOffset(System.Collections.Specialized.NameValueCollection q, string offsetKey, string sizeKey, int index)
    {
        if (!int.TryParse(q[sizeKey], out var size) || size <= 0) return false;
        if (q[offsetKey] is { } offset && !int.TryParse(offset, out _)) return false;
        q[offsetKey] = (index * size).ToString();
        return true;
    }

    /// <summary>VTEX IO: <c>extensions={"persistedQuery":{...},"variables":"&lt;base64 de JSON con from/to&gt;"}</c>.</summary>
    private static bool TryGraphQlVariables(System.Collections.Specialized.NameValueCollection q, int index)
    {
        if (q["extensions"] is not { } raw) return false;
        try
        {
            if (JsonNode.Parse(raw) is not JsonObject extensions || extensions["variables"]?.GetValue<string>() is not { } encoded) return false;
            if (JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))) is not JsonObject variables) return false;
            if (variables["from"]?.GetValue<int>() is not { } from || variables["to"]?.GetValue<int>() is not { } to || to < from) return false;

            var size = to - from + 1;
            variables["from"] = index * size;
            variables["to"] = index * size + size - 1;
            extensions["variables"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(variables.ToJsonString()));
            q["extensions"] = extensions.ToJsonString();
            return true;
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string Rebuild(Uri uri, System.Collections.Specialized.NameValueCollection query)
    {
        var parts = query.AllKeys
            .Where(k => k is not null)
            .SelectMany(k => query.GetValues(k)!.Select(v => $"{Uri.EscapeDataString(k!)}={Uri.EscapeDataString(v)}"));
        return new UriBuilder(uri) { Query = string.Join("&", parts) }.Uri.AbsoluteUri;
    }
}
