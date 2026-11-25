using System.Net;
using PrecioHerramientas.Core.Abstractions;
using PrecioHerramientas.Core.Entities;
using PrecioHerramientas.Core.Services;

namespace PrecioHerramientas.Core.Affiliates;

/// <summary>Datos de la visita que hizo clic.</summary>
public sealed record ClickVisitor(IPAddress? Ip, string? Referer, string? UserAgent);

/// <summary>
/// <c>/ir/...</c>: registra el clic (salvo robots) y devuelve el link de salida hacia la tienda,
/// ya convertido en link de afiliado si la tienda tiene programa configurado. Solo redirige a
/// URLs guardadas por los scrapers, nunca a una dirección recibida en la petición.
/// </summary>
public sealed class ClickRedirectService(IClickRepository clicks, IUnitOfWork unitOfWork, IAffiliateLinkService links, IClock clock)
{
    /// <summary>Link de salida para una oferta, o null si no existe.</summary>
    public async Task<string?> ForOfferAsync(int offerId, ClickVisitor visitor, CancellationToken ct = default)
    {
        var target = await clicks.FindOfferTargetAsync(offerId, ct);
        if (target is null) return null;

        var clickRef = await RecordAsync(target.StoreId, offerId, target.ProductId, visitor, ct) ?? $"o{offerId}";
        return links.BuildLink(target.StoreKey, target.Url, clickRef);
    }

    /// <summary>Link de salida a la portada de una tienda, o null si no existe o está inactiva.</summary>
    public async Task<string?> ForStoreAsync(string storeKey, ClickVisitor visitor, CancellationToken ct = default)
    {
        var target = await clicks.FindStoreTargetAsync(storeKey, ct);
        if (target is null) return null;

        var clickRef = await RecordAsync(target.StoreId, null, null, visitor, ct) ?? $"s{target.StoreId}";
        return links.BuildLink(target.StoreKey, target.Url, clickRef);
    }

    /// <summary>Guarda el clic y devuelve su id como referencia; null si es un robot (no se cuenta).</summary>
    private async Task<string?> RecordAsync(int storeId, int? offerId, int? productId, ClickVisitor visitor, CancellationToken ct)
    {
        if (ClickPrivacy.IsBot(visitor.UserAgent)) return null;

        var click = new Click
        {
            StoreId = storeId,
            OfferId = offerId,
            ProductId = productId,
            ClickedAt = clock.UtcNow,
            AnonymizedIp = ClickPrivacy.AnonymizeIp(visitor.Ip),
            Referer = visitor.Referer is { Length: > FieldLimits.Referer } tooLong ? tooLong[..FieldLimits.Referer] : visitor.Referer,
        };
        clicks.Add(click);
        await unitOfWork.SaveChangesAsync(ct);
        return click.Id.ToString();
    }
}
