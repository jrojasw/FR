using System.Text.RegularExpressions;
using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Core.Matching;

/// <summary>
/// Extrae el modelo / número de parte desde el título cuando la tienda no lo informa.
/// Ej.: "Taladro Percutor Inalámbrico 20V DCD778D2 DeWalt" → "DCD778D2".
/// </summary>
public static partial class ModelExtractor
{
    public static string? Extract(string title, string? brand = null)
    {
        var explicitModel = ExplicitModel().Match(title);
        if (explicitModel.Success)
        {
            var normalized = TextNormalizer.NormalizeModel(explicitModel.Groups["model"].Value);
            if (normalized is not null) return normalized;
        }

        var brandTokens = brand is null
            ? new HashSet<string>()
            : TextNormalizer.Tokenize(brand).Select(t => t.ToUpperInvariant()).ToHashSet();

        var plain = TextNormalizer.RemoveDiacritics(title);
        string? best = null;

        // Modelos escritos con espacio: "GWS 850", "HR 2470". Se exigen mayúsculas para no unir palabras comunes.
        foreach (Match split in SplitModel().Matches(plain))
        {
            var num = split.Groups["num"].Value;
            if (num.Any(char.IsAsciiLetter) && Measurement().IsMatch(num)) continue;   // "GWS 20V" no es modelo
            var joined = TextNormalizer.NormalizeModel(split.Groups["prefix"].Value + split.Groups["num"].Value);
            if (joined is not null && !brandTokens.Contains(split.Groups["prefix"].Value) && (best is null || Score(joined) > Score(best)))
                best = joined;
        }

        foreach (Match token in Token().Matches(plain))
        {
            var value = token.Value.Trim('-', '.', '/');
            if (Measurement().IsMatch(value)) continue;

            var normalized = TextNormalizer.NormalizeModel(value);
            if (normalized is null || normalized.Length < 4) continue;
            if (!normalized.Any(char.IsAsciiLetter)) continue;    // solo números: cantidades, años
            if (brandTokens.Contains(normalized)) continue;

            if (best is null || Score(normalized) > Score(best))
                best = normalized;
        }
        return best;
    }

    // Más dígitos y más largo = más probable que sea el número de parte.
    private static int Score(string candidate) =>
        candidate.Count(char.IsAsciiDigit) * 2 + candidate.Length;

    [GeneratedRegex(@"\b(?:modelo|mod\.?|model|ref\.?|sku)\s*[:#]?\s*(?<model>[A-Z0-9][A-Z0-9\-./]{2,}(?:\s\d[A-Z0-9\-./]*)?)", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitModel();

    [GeneratedRegex(@"\b(?<prefix>[A-Z]{2,5})[ -](?<num>\d{2,5}[A-Z]{0,3})\b")]
    private static partial Regex SplitModel();

    [GeneratedRegex(@"[A-Za-z0-9][A-Za-z0-9\-./]*[A-Za-z0-9]")]
    private static partial Regex Token();

    // Medidas y especificaciones que no son modelos: 20V, 650W, 13mm, 2.0Ah, 1/2", 3000rpm, 4.5", 115mm, 10pzs...
    [GeneratedRegex(@"^(\d+([.,/]\d+)*)(v|vdc|vac|w|kw|kva|mm|cm|mts?|m|ah|mah|rpm|hp|cc|kg|gr?|oz|lbs?|ml|lts?|l|psi|bar|nm|in|pulg|pzs?|pcs|piezas|un|und|unid|x\d+|hz|db)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Measurement();
}
