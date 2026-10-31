using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrecioHerramientas.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Elimina las tiendas ficticias "demo-norte" y "demo-sur" con todos sus datos, en el
    /// orden que exigen las claves foráneas: historial → ofertas → ejecuciones → tiendas.
    /// Luego borra los productos que solo tenían ofertas demo, recalcula mejor precio y
    /// cantidad de tiendas de los que siguen en otras tiendas, y quita marcas y categorías
    /// que quedaron sin productos. Si las tiendas demo no existen, no hace nada.
    /// </summary>
    public partial class RemoveDemoStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE demo_store_ids AS
                    SELECT id FROM stores WHERE key IN ('demo-norte', 'demo-sur');

                CREATE TEMP TABLE demo_product_ids AS
                    SELECT DISTINCT product_id AS id FROM offers
                    WHERE store_id IN (SELECT id FROM demo_store_ids);

                DELETE FROM price_points
                WHERE offer_id IN (SELECT id FROM offers WHERE store_id IN (SELECT id FROM demo_store_ids));

                DELETE FROM offers WHERE store_id IN (SELECT id FROM demo_store_ids);

                DELETE FROM scrape_runs WHERE store_id IN (SELECT id FROM demo_store_ids);

                DELETE FROM stores WHERE id IN (SELECT id FROM demo_store_ids);

                -- Productos que ya no tienen ofertas de ninguna otra tienda.
                DELETE FROM products p
                WHERE p.id IN (SELECT id FROM demo_product_ids)
                  AND NOT EXISTS (SELECT 1 FROM offers o WHERE o.product_id = p.id);

                -- Los que siguen en otras tiendas: recalcular mejor precio y cantidad de tiendas.
                UPDATE products p SET
                    offer_count = (SELECT count(*) FROM offers o WHERE o.product_id = p.id AND o.is_available),
                    best_price = (SELECT min(o.price) FROM offers o WHERE o.product_id = p.id AND o.is_available),
                    updated_at = now() AT TIME ZONE 'utc'
                WHERE p.id IN (SELECT id FROM demo_product_ids);

                -- Marcas y categorías que quedaron sin productos (se vuelven a crear si una tienda las trae).
                DELETE FROM brands b WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p.brand_id = b.id);
                DELETE FROM categories c WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p.category_id = c.id);

                DROP TABLE demo_product_ids;
                DROP TABLE demo_store_ids;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los datos borrados no se pueden reconstruir: revertir esta migración no hace nada.
        }
    }
}
