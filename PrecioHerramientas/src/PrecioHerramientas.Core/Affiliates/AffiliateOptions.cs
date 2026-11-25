namespace PrecioHerramientas.Core.Affiliates;

/// <summary>
/// Programas de afiliados por tienda (sección <c>Affiliates</c> de appsettings). Los ids nunca
/// van en el código; en producción se cargan con variables de entorno, p. ej.
/// <c>Affiliates__Stores__paris__AffiliateId</c>.
/// </summary>
public sealed class AffiliateOptions
{
    public const string SectionName = "Affiliates";

    /// <summary>Configuración por clave de tienda ("paris", "mercadolibre"...). Sin entrada = link directo.</summary>
    public Dictionary<string, StoreAffiliateOptions> Stores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StoreAffiliateOptions
{
    /// <summary>Programa que arma el link: "Awin", "QueryParameters" o "Direct".</summary>
    public string Program { get; set; } = AffiliatePrograms.Direct;

    /// <summary>Awin: id de la tienda en Awin (awinmid).</summary>
    public string? MerchantId { get; set; }

    /// <summary>Awin: id de afiliado (awinaffid).</summary>
    public string? AffiliateId { get; set; }

    /// <summary>QueryParameters: parámetros de tracking que se agregan a la URL del producto.</summary>
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
}

public static class AffiliatePrograms
{
    public const string Awin = "Awin";
    public const string QueryParameters = "QueryParameters";
    public const string Direct = "Direct";
}
