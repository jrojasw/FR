using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PrecioHerramientas.Core.Affiliates;

/// <summary>Elige, según la configuración de la tienda, el programa que arma el link de salida.</summary>
public interface IAffiliateLinkService
{
    string BuildLink(string storeKey, string destinationUrl, string clickRef);
}

public sealed class AffiliateLinkService(
    IEnumerable<IAffiliateLinkBuilder> builders,
    IOptionsMonitor<AffiliateOptions> options,
    ILogger<AffiliateLinkService> logger) : IAffiliateLinkService
{
    private readonly Dictionary<string, IAffiliateLinkBuilder> _builders =
        builders.ToDictionary(b => b.Program, StringComparer.OrdinalIgnoreCase);

    // Cada problema de configuración se avisa una vez por tienda, no en cada clic.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _warned = new(StringComparer.OrdinalIgnoreCase);

    public string BuildLink(string storeKey, string destinationUrl, string clickRef)
    {
        if (!options.CurrentValue.Stores.TryGetValue(storeKey, out var store)
            || string.Equals(store.Program, AffiliatePrograms.Direct, StringComparison.OrdinalIgnoreCase))
            return destinationUrl;

        if (!_builders.TryGetValue(store.Program, out var builder))
        {
            if (_warned.TryAdd(storeKey, true))
                logger.LogWarning("Afiliados: programa \"{Program}\" desconocido para {Store}; se usa el link directo", store.Program, storeKey);
            return destinationUrl;
        }

        var request = new AffiliateLinkRequest(storeKey, destinationUrl, clickRef);
        if (builder.Build(request, store) is { } link) return link;

        // Mejor un clic sin comisión que un link roto.
        if (_warned.TryAdd(storeKey, true))
            logger.LogWarning("Afiliados: a {Store} ({Program}) le faltan datos en la configuración; se usa el link directo", storeKey, store.Program);
        return destinationUrl;
    }
}
