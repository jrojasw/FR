using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PrecioHerramientas.Infrastructure.Persistence;

public static class ConnectionStrings
{
    /// <summary>
    /// Cadena de conexión a PostgreSQL. Si existe <c>DATABASE_URL</c> (formato
    /// <c>postgres://usuario:clave@host:puerto/base</c>, el que entregan Railway,
    /// Render y similares) tiene prioridad; si no, se usa <c>ConnectionStrings:Default</c>.
    /// </summary>
    public static string Resolve(IConfiguration configuration)
    {
        if (configuration["DATABASE_URL"] is { Length: > 0 } url)
            return FromUrl(url);

        return configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:Default' o la variable DATABASE_URL.");
    }

    public static string FromUrl(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme is not ("postgres" or "postgresql"))
            throw new InvalidOperationException($"DATABASE_URL debe empezar con postgres://, no {uri.Scheme}://.");

        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        // Respeta ?sslmode=require y similares si vienen en la URL.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(kv[1], ignoreCase: true, out var sslMode))
                builder.SslMode = sslMode;
        }
        return builder.ConnectionString;
    }
}
