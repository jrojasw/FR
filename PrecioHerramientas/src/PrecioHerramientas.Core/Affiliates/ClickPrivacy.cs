using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace PrecioHerramientas.Core.Affiliates;

public static partial class ClickPrivacy
{
    /// <summary>IPv4 con el último octeto en 0 (/24); IPv6 recortada a /48. Null si no hay IP válida.</summary>
    public static string? AnonymizeIp(IPAddress? address)
    {
        if (address is null) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            bytes[3] = 0;
        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            Array.Clear(bytes, 6, bytes.Length - 6);
        else
            return null;
        return new IPAddress(bytes).ToString();
    }

    /// <summary>
    /// Robots de buscadores, previsualizaciones de redes sociales y herramientas automáticas.
    /// Se les redirige igual, pero su clic no se cuenta.
    /// </summary>
    public static bool IsBot(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent) || BotUserAgent().IsMatch(userAgent);

    [GeneratedRegex(@"bot\b|bot/|crawl|spider|slurp|facebookexternalhit|whatsapp|telegram|preview|curl/|wget/|python-requests|httpclient|headless|lighthouse|pingdom|uptime",
        RegexOptions.IgnoreCase)]
    private static partial Regex BotUserAgent();
}
