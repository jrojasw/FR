using Hangfire.Dashboard;

namespace PrecioHerramientas.Web.Infrastructure;

/// <summary>Protege el dashboard de Hangfire con <see cref="BasicAuth"/>.</summary>
public sealed class HangfireBasicAuthFilter(string? username, string? password) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        BasicAuth.IsAuthorized(context.GetHttpContext(), username, password, "Hangfire");
}
