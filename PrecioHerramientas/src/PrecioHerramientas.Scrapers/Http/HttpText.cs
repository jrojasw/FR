using System.Text;

namespace PrecioHerramientas.Scrapers.Http;

/// <summary>
/// Convierte una respuesta en texto sin dañar tildes ni eñes. Hay tiendas que declaran
/// <c>charset=ISO-8859-1</c> pero envían UTF-8: confiar en el encabezado produce "VÃ¡lvula".
/// Si los bytes son UTF-8 válido se leen como UTF-8; si no, con la codificación declarada
/// (o Latin-1, que nunca falla).
/// </summary>
public static class HttpText
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static async Task<string> ReadAsync(HttpContent content, CancellationToken ct) =>
        Decode(await content.ReadAsByteArrayAsync(ct), content.Headers.ContentType?.CharSet);

    public static string Decode(byte[] bytes, string? declaredCharset)
    {
        var span = bytes.AsSpan();
        if (span.StartsWith(Encoding.UTF8.Preamble)) span = span[Encoding.UTF8.Preamble.Length..];
        try
        {
            return StrictUtf8.GetString(span);
        }
        catch (DecoderFallbackException)
        {
            // De verdad no es UTF-8: se respeta lo declarado.
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(declaredCharset))
                return Encoding.GetEncoding(declaredCharset.Trim('"', ' ')).GetString(span);
        }
        catch (ArgumentException)
        {
            // Codificación desconocida para .NET.
        }
        return Encoding.Latin1.GetString(span);
    }
}
