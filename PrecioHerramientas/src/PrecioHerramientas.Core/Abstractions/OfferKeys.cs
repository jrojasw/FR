namespace PrecioHerramientas.Core.Abstractions;

/// <summary>Identifica una oferta dentro de una tienda: id externo + sucursal.</summary>
public static class OfferKeys
{
    public static string For(string externalId, string? branchCode) => $"{externalId}\u001f{branchCode ?? ""}";
}
