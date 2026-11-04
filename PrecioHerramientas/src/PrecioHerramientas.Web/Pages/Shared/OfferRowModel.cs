using PrecioHerramientas.Core.Queries;

namespace PrecioHerramientas.Web.Pages.Shared;

/// <param name="IsBest">Resalta el precio (mejor precio del producto).</param>
/// <param name="MoreCount">Publicaciones agrupadas bajo esta fila (botón "Ver N más").</param>
/// <param name="MoreTarget">Id del bloque desplegable con esas publicaciones.</param>
public sealed record OfferRowModel(OfferRow Row, bool IsBest, int MoreCount, string? MoreTarget);
