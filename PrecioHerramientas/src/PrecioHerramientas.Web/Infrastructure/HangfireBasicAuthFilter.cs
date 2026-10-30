using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;

namespace PrecioHerramientas.Web.Infrastructure;

/// <summary>
/// Protege el dashboard de Hangfire con autenticación HTTP Basic. Sin credenciales
/// configuradas, el dashboard queda cerrado. Debe servirse detrás de HTTPS.
/// </summary>
public sealed class HangfireBasicAuthFilter(string? username, string? password) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return false;

        if (AuthenticationHeaderValue.TryParse(http.Request.Headers.Authorization, out var header)
            && string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
            && header.Parameter is not null
            && TryDecode(header.Parameter, out var user, out var pass)
            && FixedTimeEquals(user, username) & FixedTimeEquals(pass, password))
            return true;

        // Hace que el navegador muestre el cuadro de usuario y contraseña.
        http.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire\", charset=\"UTF-8\"";
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
