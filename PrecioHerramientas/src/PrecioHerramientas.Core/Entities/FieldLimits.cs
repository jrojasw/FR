namespace PrecioHerramientas.Core.Entities;

/// <summary>
/// Largo máximo de cada columna de texto. Lo usan la configuración de EF Core (tamaño de
/// la columna) y la ingesta (que trunca antes de guardar), así que no pueden divergir.
/// </summary>
public static class FieldLimits
{
    public const int StoreKey = 50;
    public const int StoreName = 200;
    public const int StoreUrl = 300;
    public const int Url = 1000;

    public const int BrandName = 200;
    public const int CategoryName = 200;
    public const int Slug = 200;

    public const int ProductName = 500;
    public const int ProductSlug = 200;
    public const int Model = 200;
    public const int ProductEan = 14;
    public const int SearchText = 1500;

    public const int ExternalId = 200;
    public const int OfferTitle = 500;
    public const int BrandRaw = 200;
    public const int ModelRaw = 200;
    public const int OfferEan = 200;
}
