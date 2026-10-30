using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PrecioHerramientas.Core.Text;

public static partial class TextNormalizer
{
    /// <summary>Quita tildes y diacríticos ("Taladro Percutor Ñ" → "Taladro Percutor N").</summary>
    public static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Texto apto para búsqueda: minúsculas, sin tildes, solo letras/dígitos separados por un espacio.</summary>
    public static string ToSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var plain = RemoveDiacritics(value).ToLowerInvariant();
        return NonAlphanumeric().Replace(plain, " ").Trim();
    }

    /// <summary>Divide un texto de búsqueda en términos normalizados.</summary>
    public static IReadOnlyList<string> Tokenize(string? value) =>
        ToSearchText(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static string Slugify(string value, int maxLength = 120)
    {
        var slug = ToSearchText(value).Replace(' ', '-');
        if (slug.Length > maxLength)
            slug = slug[..maxLength].TrimEnd('-');
        return slug.Length == 0 ? "producto" : slug;
    }

    /// <summary>Colapsa espacios repetidos y recorta.</summary>
    public static string CleanWhitespace(string value) => Whitespace().Replace(value, " ").Trim();

    /// <summary>
    /// Convierte un precio en formato chileno a número: "$12.990", "$ 1.299.990",
    /// "12990", "12.990,50". Devuelve null si no se reconoce un precio positivo.
    /// </summary>
    public static decimal? ParseClpPrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = PriceCandidate().Match(text);
        if (!match.Success) return null;

        var raw = match.Value.Trim('.', ',');
        int lastDot = raw.LastIndexOf('.');
        int lastComma = raw.LastIndexOf(',');
        int lastSep = Math.Max(lastDot, lastComma);

        string integerPart = raw, decimalPart = "";
        if (lastSep >= 0)
        {
            int digitsAfter = raw.Length - lastSep - 1;
            bool bothSeparators = lastDot >= 0 && lastComma >= 0;
            // El último separador es decimal si hay ambos tipos, o si le siguen 1-2 dígitos.
            if (bothSeparators || digitsAfter is 1 or 2)
            {
                integerPart = raw[..lastSep];
                decimalPart = raw[(lastSep + 1)..];
            }
        }

        integerPart = integerPart.Replace(".", "").Replace(",", "");
        var number = decimalPart.Length > 0 ? $"{integerPart}.{decimalPart}" : integerPart;
        if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return null;

        return value > 0 ? value : null;
    }

    /// <summary>
    /// Normaliza un código de barras a GTIN-13 validando el dígito verificador.
    /// Acepta EAN-8, UPC-A (12), EAN-13 y GTIN-14 con indicador 0.
    /// </summary>
    public static string? NormalizeEan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());

        if (digits.Length == 14 && digits[0] == '0') digits = digits[1..];
        if (digits.Length == 12) digits = "0" + digits;
        if (digits.Length is not (8 or 13)) return null;
        if (digits.All(c => c == '0')) return null;
        if (!HasValidGtinCheckDigit(digits)) return null;

        return digits.Length == 8 ? digits.PadLeft(13, '0') : digits;
    }

    private static bool HasValidGtinCheckDigit(string digits)
    {
        int sum = 0;
        // Desde el dígito anterior al verificador hacia la izquierda, pesos 3,1,3,1...
        for (int i = digits.Length - 2, weight = 3; i >= 0; i--, weight = weight == 3 ? 1 : 3)
            sum += (digits[i] - '0') * weight;
        int check = (10 - sum % 10) % 10;
        return check == digits[^1] - '0';
    }

    /// <summary>Modelo normalizado: mayúsculas, sin espacios, guiones ni puntos ("DCD-771 C2" → "DCD771C2").</summary>
    public static string? NormalizeModel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = NonAlphanumeric().Replace(RemoveDiacritics(value).ToUpperInvariant(), "");
        return normalized.Length >= 3 && normalized.Any(char.IsAsciiDigit) ? normalized : null;
    }

    /// <summary>Texto en mayúsculas sin espacios ni signos, para buscar modelos dentro de un título.</summary>
    public static string ToCompactUpper(string? value) =>
        string.IsNullOrEmpty(value) ? "" : NonAlphanumeric().Replace(RemoveDiacritics(value).ToUpperInvariant(), "");

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.IgnoreCase)]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\d[\d.,]*")]
    private static partial Regex PriceCandidate();
}
