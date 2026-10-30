using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Core.Matching;

/// <summary>
/// Marcas de herramientas conocidas y sus variantes de escritura, para unificar
/// "Black & Decker", "BLACK+DECKER" y "Black and Decker" bajo un mismo nombre.
/// </summary>
public static class BrandCatalog
{
    private static readonly (string Canonical, string[] Aliases)[] Brands =
    [
        ("DeWalt", ["dewalt", "de walt"]),
        ("Makita", ["makita"]),
        ("Bosch", ["bosch"]),
        ("Black+Decker", ["black decker", "black and decker", "blackdecker", "black y decker"]),
        ("Stanley", ["stanley"]),
        ("Milwaukee", ["milwaukee"]),
        ("Einhell", ["einhell"]),
        ("Truper", ["truper"]),
        ("Pretul", ["pretul"]),
        ("Bauker", ["bauker"]),
        ("Ubermann", ["ubermann"]),
        ("Skil", ["skil"]),
        ("Dremel", ["dremel"]),
        ("Kärcher", ["karcher", "kaercher"]),
        ("Ryobi", ["ryobi"]),
        ("Metabo", ["metabo"]),
        ("HiKOKI", ["hikoki", "hitachi"]),
        ("Total", ["total tools", "total"]),
        ("Ingco", ["ingco"]),
        ("Redline", ["redline"]),
        ("Irwin", ["irwin"]),
        ("Bahco", ["bahco"]),
        ("Tramontina", ["tramontina"]),
        ("Worx", ["worx"]),
        ("Hyundai", ["hyundai"]),
        ("Gamma", ["gamma"]),
        ("Kendo", ["kendo"]),
        ("Uyustools", ["uyustools", "uyus tools"]),
        ("Toolmen", ["toolmen"]),
        ("Dowen Pagio", ["dowen pagio", "dowen"]),
        ("Rexon", ["rexon"]),
        ("Indura", ["indura"]),
        ("Lusqtoff", ["lusqtoff"]),
        ("Husqvarna", ["husqvarna"]),
        ("Stihl", ["stihl"]),
        ("Fischer", ["fischer"]),
        ("Wadfow", ["wadfow"]),
        ("Barovo", ["barovo"]),
        ("Mikels", ["mikels"]),
        ("Craftsman", ["craftsman"]),
    ];

    // Alias normalizado → nombre canónico, ordenado de más largo a más corto para
    // que "total tools" gane sobre "total".
    private static readonly (string Alias, string Canonical)[] AliasIndex = Brands
        .SelectMany(b => b.Aliases.Select(a => (Alias: TextNormalizer.ToSearchText(a), b.Canonical)))
        .OrderByDescending(x => x.Alias.Length)
        .ToArray();

    /// <summary>
    /// Devuelve el nombre canónico de la marca. Usa la marca informada por la tienda
    /// y, si no existe, busca una marca conocida dentro del título.
    /// </summary>
    public static string? Resolve(string? rawBrand, string? title)
    {
        if (!string.IsNullOrWhiteSpace(rawBrand))
        {
            var normalized = TextNormalizer.ToSearchText(rawBrand);
            foreach (var (alias, canonical) in AliasIndex)
            {
                if (normalized == alias || normalized.Replace(" ", "") == alias.Replace(" ", ""))
                    return canonical;
            }
            // Marca desconocida: se respeta lo que informa la tienda, con formato prolijo.
            if (normalized.Length > 0 && !IsGenericBrand(normalized))
                return TextNormalizer.CleanWhitespace(rawBrand.Trim());
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var padded = $" {TextNormalizer.ToSearchText(title)} ";
            foreach (var (alias, canonical) in AliasIndex)
            {
                if (padded.Contains($" {alias} ", StringComparison.Ordinal))
                    return canonical;
            }
        }

        return null;
    }

    private static bool IsGenericBrand(string normalized) =>
        normalized is "generico" or "generica" or "sin marca" or "otras marcas" or "otra" or "na" or "n a";
}
