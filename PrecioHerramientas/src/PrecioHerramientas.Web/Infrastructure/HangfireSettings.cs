namespace PrecioHerramientas.Web.Infrastructure;

public sealed class HangfireSettings
{
    public const string SectionName = "Hangfire";

    /// <summary>Si es false, el sitio no procesa trabajos (útil para escalar la web aparte).</summary>
    public bool RunServer { get; set; } = true;

    public int WorkerCount { get; set; } = 2;
    public string DashboardPath { get; set; } = "/hangfire";
    public string? DashboardUser { get; set; }
    public string? DashboardPassword { get; set; }

    /// <summary>Zona horaria en que se interpretan las expresiones cron.</summary>
    public string TimeZone { get; set; } = "America/Santiago";
}
