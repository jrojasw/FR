using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Core.Queries;

/// <summary>Una fila de la tabla de precios: la oferta y cómo se presenta su tienda.</summary>
/// <param name="StoreLabel">Tienda a mostrar ("Sodimac", o "Imperial (vía Sodimac)").</param>
/// <param name="SellerNote">"Vendido por X" cuando vende un tercero que no seguimos como tienda.</param>
public sealed record OfferRow(OfferView Offer, string StoreLabel, string? SellerNote);

/// <summary>Publicaciones de una misma tienda, vendedor y sucursal: la más conveniente y el resto.</summary>
public sealed record OfferGroup(OfferRow Primary, IReadOnlyList<OfferRow> More);

/// <summary>
/// Ordena las ofertas de un producto para la ficha:
/// <list type="bullet">
/// <item>Una fila por tienda + vendedor + sucursal (la de menor precio con stock); las demás
/// publicaciones del grupo (otros SKU del mismo vendedor) quedan en <see cref="OfferGroup.More"/>.</item>
/// <item>Si el vendedor de una publicación es otra tienda que también seguimos (p. ej. Imperial
/// vendiendo en sodimac.cl), se descarta cuando esa tienda ya tiene una oferta del mismo
/// producto al mismo precio; si no, se presenta como "Imperial (vía Sodimac)".</item>
/// </list>
/// </summary>
public static class OfferGrouping
{
    public static IReadOnlyList<OfferGroup> Build(IReadOnlyList<OfferView> offers)
    {
        var stores = offers.Select(o => (o.StoreKey, o.StoreName)).Distinct().ToList();
        var rows = new List<(OfferRow Row, string Group)>();

        foreach (var offer in offers)
        {
            var seller = offer.Seller?.Trim();
            string label = offer.StoreName, sellerKey = "";
            string? note = null;

            if (!string.IsNullOrEmpty(seller) && !SameName(seller, offer.StoreName))
            {
                var tracked = stores.FirstOrDefault(s => s.StoreKey != offer.StoreKey && SameName(seller, s.StoreName));
                if (tracked.StoreKey is not null)
                {
                    // La misma oferta ya aparece desde la tienda del vendedor: no se repite.
                    if (offers.Any(o => o.StoreKey == tracked.StoreKey && o.Price == offer.Price))
                        continue;
                    label = $"{tracked.StoreName} (vía {offer.StoreName})";
                    sellerKey = tracked.StoreKey;
                }
                else
                {
                    note = $"Vendido por {seller}";
                    sellerKey = TextNormalizer.ToSearchText(seller);
                }
            }
            rows.Add((new OfferRow(offer, label, note), $"{offer.StoreKey}|{sellerKey}|{offer.BranchName}"));
        }

        return rows
            .GroupBy(r => r.Group)
            .Select(g =>
            {
                var ordered = g.Select(r => r.Row).OrderByDescending(r => r.Offer.IsAvailable).ThenBy(r => r.Offer.Price).ToList();
                return new OfferGroup(ordered[0], ordered.Skip(1).ToList());
            })
            .OrderByDescending(g => g.Primary.Offer.IsAvailable)
            .ThenBy(g => g.Primary.Offer.Price)
            .ToList();
    }

    /// <summary>"SODIMAC", "Sodimac" y "Sodimac.cl" son la misma tienda; "Imperial S.A." es Imperial.</summary>
    private static bool SameName(string a, string b)
    {
        var x = TextNormalizer.ToSearchText(a).Replace(" ", "");
        var y = TextNormalizer.ToSearchText(b).Replace(" ", "");
        return x.Length > 0 && y.Length > 0 && (x.Contains(y, StringComparison.Ordinal) || y.Contains(x, StringComparison.Ordinal));
    }
}
