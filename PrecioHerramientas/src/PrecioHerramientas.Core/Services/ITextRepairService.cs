namespace PrecioHerramientas.Core.Services;

/// <summary>Un texto que cambia: tabla, fila, campo, antes y después.</summary>
public sealed record TextChange(string Table, long Id, string Field, string Before, string After);

/// <param name="Changes">Todos los cambios, primero los nombres de productos.</param>
/// <param name="Merges">Marcas, categorías o productos que se fusionan con su versión ya correcta.</param>
public sealed record TextRepairReport(IReadOnlyList<TextChange> Changes, IReadOnlyList<string> Merges)
{
    public int Count => Changes.Count;
}

/// <summary>
/// Repara tildes y eñes dañadas (mojibake) en los datos ya guardados. Nunca borra productos
/// ni historial: al fusionar, mueve ofertas y clics al registro que se conserva.
/// </summary>
public interface ITextRepairService
{
    /// <summary>Qué cambiaría, sin guardar nada.</summary>
    Task<TextRepairReport> PreviewAsync(CancellationToken ct = default);

    /// <summary>Aplica los cambios en una sola transacción.</summary>
    Task<TextRepairReport> ApplyAsync(CancellationToken ct = default);
}
