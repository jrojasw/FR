using System.Globalization;

namespace PrecioHerramientas.Web.Infrastructure;

public static class Format
{
    private static readonly NumberFormatInfo Clp = new() { NumberGroupSeparator = ".", NumberDecimalSeparator = "," };
    private static readonly TimeZoneInfo Chile = FindChile();

    /// <summary>Formato de precio chileno: $12.990.</summary>
    public static string Price(decimal? value) =>
        value is null ? "—" : "$" + Math.Round(value.Value, 0, MidpointRounding.AwayFromZero).ToString("#,0", Clp);

    public static string Date(DateTime utc) => ToChile(utc).ToString("dd-MM-yyyy");

    public static string DateTime(DateTime utc) => ToChile(utc).ToString("dd-MM-yyyy HH:mm");

    private static System.DateTime ToChile(System.DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.SpecifyKind(utc, DateTimeKind.Utc), Chile);

    private static TimeZoneInfo FindChile()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Santiago"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
