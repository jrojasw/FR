namespace PrecioHerramientas.Web.Pages.Shared;

public sealed record PaginationModel(int Page, int TotalPages, Func<int, string> UrlFor);
