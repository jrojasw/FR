namespace PrecioHerramientas.Core.Entities;

public class Brand
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
