using System.Web;

namespace PrecioHerramientas.Core.Affiliates;

/// <param name="StoreKey">Tienda de destino.</param>
/// <param name="DestinationUrl">URL de la publicación (o portada) en la tienda.</param>
/// <param name="ClickRef">Referencia del clic, para cruzarla con los reportes del programa.</param>
public sealed record AffiliateLinkRequest(string StoreKey, string DestinationUrl, string ClickRef);

/// <summary>Arma el link de afiliado de un programa. Una implementación por programa.</summary>
public interface IAffiliateLinkBuilder
{
    /// <summary>Nombre del programa en la configuración (<see cref="AffiliatePrograms"/>).</summary>
    string Program { get; }

    /// <summary>Link final, o null si a la configuración de la tienda le faltan datos.</summary>
    string? Build(AffiliateLinkRequest request, StoreAffiliateOptions options);
}

/// <summary>Tiendas sin programa: la URL tal cual.</summary>
public sealed class DirectLinkBuilder : IAffiliateLinkBuilder
{
    public string Program => AffiliatePrograms.Direct;

    public string? Build(AffiliateLinkRequest request, StoreAffiliateOptions options) => request.DestinationUrl;
}

/// <summary>
/// Awin (p. ej. Paris):
/// <c>https://www.awin1.com/cread.php?awinmid={mid}&amp;awinaffid={affid}&amp;clickref={id}&amp;ued={url codificada}</c>.
/// </summary>
public sealed class AwinLinkBuilder : IAffiliateLinkBuilder
{
    public string Program => AffiliatePrograms.Awin;

    public string? Build(AffiliateLinkRequest request, StoreAffiliateOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MerchantId) || string.IsNullOrWhiteSpace(options.AffiliateId)) return null;
        return "https://www.awin1.com/cread.php"
            + $"?awinmid={Uri.EscapeDataString(options.MerchantId.Trim())}"
            + $"&awinaffid={Uri.EscapeDataString(options.AffiliateId.Trim())}"
            + $"&clickref={Uri.EscapeDataString(request.ClickRef)}"
            + $"&ued={Uri.EscapeDataString(request.DestinationUrl)}";
    }
}

/// <summary>
/// Programas que se activan agregando parámetros a la URL del producto (p. ej. Mercado Libre).
/// Conserva los parámetros que ya tenga la URL y reemplaza los que coincidan con los configurados.
/// Un valor "{clickref}" se reemplaza por la referencia del clic.
/// </summary>
public sealed class QueryParametersLinkBuilder : IAffiliateLinkBuilder
{
    public string Program => AffiliatePrograms.QueryParameters;

    public string? Build(AffiliateLinkRequest request, StoreAffiliateOptions options)
    {
        var configured = options.Parameters.Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value)).ToList();
        if (configured.Count == 0) return null;

        var uri = new UriBuilder(request.DestinationUrl);
        var query = HttpUtility.ParseQueryString(uri.Query);
        foreach (var (name, value) in configured)
            query[name] = value.Replace("{clickref}", request.ClickRef, StringComparison.OrdinalIgnoreCase);

        uri.Query = string.Join("&", query.AllKeys
            .Where(k => k is not null)
            .SelectMany(k => query.GetValues(k)!.Select(v => $"{Uri.EscapeDataString(k!)}={Uri.EscapeDataString(v)}")));
        // UriBuilder agrega el puerto por defecto en algunas URLs; se conserva el original.
        return uri.Uri.AbsoluteUri;
    }
}
