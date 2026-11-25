namespace PrecioHerramientas.Core.Entities;

/// <summary>
/// Un visitante hizo clic en "Ver" / "Ir a la tienda" (pasando por <c>/ir/...</c>).
/// Sirve para medir el tráfico enviado a cada tienda y cuadrarlo con los programas de afiliados.
/// </summary>
public class Click
{
    public long Id { get; set; }

    /// <summary>Oferta clicada; nulo en clics a la portada de una tienda o si la oferta se borró después.</summary>
    public int? OfferId { get; set; }
    public Offer? Offer { get; set; }

    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public DateTime ClickedAt { get; set; }

    /// <summary>IP sin su parte final (IPv4 /24, IPv6 /48): permite ver patrones sin identificar a la persona.</summary>
    public string? AnonymizedIp { get; set; }

    /// <summary>Página del sitio desde la que se hizo clic.</summary>
    public string? Referer { get; set; }
}
