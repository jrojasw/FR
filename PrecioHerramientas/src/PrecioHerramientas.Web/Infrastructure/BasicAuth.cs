using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace PrecioHerramientas.Web.Infrastructure;

/// <summary>
/// Autenticación HTTP Basic compartida por el dashboard de Hangfire y el panel de
/// administración (mismo usuario y clave: <c>Hangfire:DashboardUser/DashboardPassword</c>).
/// Sin credenciales configuradas no deja entrar a nadie. Debe servirse detrás de HTTPS.
/// </summary>
public static class BasicAuth
{
    /// <summary>
    /// True si la petición trae el usuario y la clave correctos. Si no, deja el encabezado
    /// que hace al navegador pedirlos; quien llama responde 401.
    /// </summary>
    public static bool IsAuthorized(HttpContext http, string? username, string? password, string realm)
    {
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)
            && AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization, out var header)
            && string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
            && header.Parameter is not null
            && TryDecode(header.Parameter, out var user, out var pass)
            && FixedTimeEquals(user, username) & FixedTimeEquals(pass, password))
            return true;

        http.Response.Headers.WWWAuthenticate = $"Basic realm=\"{realm}\", charset=\"UTF-8\"";
        return false;
    }

    private static bool TryDecode(string parameter, out string user, out string pass)
    {
        user = pass = "";
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parameter));
            var separator = decoded.IndexOf(':');
            if (separator < 0) return false;
            user = decoded[..separator];
            pass = decoded[(separator + 1)..];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
