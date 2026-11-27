using System.Text;
using System.Text.RegularExpressions;

namespace PrecioHerramientas.Core.Text;

/// <summary>
/// Repara texto "mojibake": UTF-8 que alguien leyó como Windows-1252 (o Latin-1).
/// "VÃ¡lvula" → "Válvula", "baterÃ­a" → "batería", "4 1/2â€³" → "4 1/2″".
/// <para>
/// Solo toca secuencias que, vueltas a bytes de Windows-1252, forman UTF-8 válido: un
/// carácter inicial (Ã, Â, â, ð…) seguido de sus bytes de continuación. Así no altera
/// texto correcto ("Válvula", "Ñuñoa", "Kärcher") y repara también textos donde solo
/// una parte está dañada. Se repite por si el texto se dañó dos veces.
/// </para>
/// </summary>
public static partial class TextRepair
{
    /// <summary>Caracteres de Windows-1252 para los bytes 0x80–0x9F (los demás bytes coinciden con Latin-1).</summary>
    private static readonly char[] Cp1252High =
    [
        '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
        '\u0090', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
    ];

    private static readonly Dictionary<char, byte> CharToByte = BuildCharToByte();

    private static Dictionary<char, byte> BuildCharToByte()
    {
        var map = new Dictionary<char, byte>();
        for (int b = 0xA0; b <= 0xFF; b++) map[(char)b] = (byte)b;
        for (int i = 0; i < Cp1252High.Length; i++)
        {
            map[Cp1252High[i]] = (byte)(0x80 + i);
            map[(char)(0x80 + i)] = (byte)(0x80 + i);   // también si se leyó como Latin-1 (control C1)
        }
        return map;
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>¿Parece tener mojibake? (rápido; para filtrar antes de reparar).</summary>
    public static bool LooksBroken(string? text) => text is not null && Sequence().IsMatch(text);

    /// <summary>Texto reparado; el mismo texto si no tenía nada que reparar. Null queda null.</summary>
    public static string? Fix(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var current = text;
        for (int pass = 0; pass < 3; pass++)
        {
            var next = Sequence().Replace(current, Decode);
            if (next == current) break;
            current = next;
        }
        return current;
    }

    private static string Decode(Match match)
    {
        var bytes = new byte[match.Length];
        for (int i = 0; i < match.Length; i++)
        {
            if (!CharToByte.TryGetValue(match.Value[i], out bytes[i])) return match.Value;
        }
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return match.Value;
        }
    }

    // Byte inicial de UTF-8 (como carácter de Windows-1252) + sus bytes de continuación 0x80–0xBF
    // (Latin-1 U+0080–U+00BF o el carácter de Windows-1252 de 0x80–0x9F).
    private const string Continuation = "[\u0080-¿€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ]";

    [GeneratedRegex("[Â-ß]" + Continuation + "|[à-ï]" + Continuation + "{2}|[ð-ô]" + Continuation + "{3}")]
    private static partial Regex Sequence();
}
