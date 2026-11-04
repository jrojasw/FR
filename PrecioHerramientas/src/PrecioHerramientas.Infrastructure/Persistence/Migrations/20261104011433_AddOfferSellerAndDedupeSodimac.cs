using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrecioHerramientas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferSellerAndDedupeSodimac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "seller",
                table: "offers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Una misma publicación de Sodimac podía quedar guardada dos veces: el id venía de
            // "skuId" al leer el JSON y de otro atributo al leer la grilla HTML de respaldo. El id
            // canónico sale ahora de la URL (/product/{productId}/{nombre}/{skuId}). Aquí se
            // fusionan los duplicados existentes: queda la oferta vista más recientemente, que
            // recibe el historial de las demás, con el id y la URL canónicos.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE sodimac_offers AS
                SELECT o.id, o.store_id, o.branch_code, o.last_seen_at, o.product_id,
                       regexp_replace(o.url, '[?#].*$', '') AS canon_url,
                       COALESCE(
                           substring(regexp_replace(o.url, '[?#].*$', '') FROM '/product/[^/]+/[^/]+/([^/]+)/?$'),
                           substring(o.url FROM '/product/([^/?#]+)'),
                           o.external_id) AS canon_id
                FROM offers o JOIN stores s ON s.id = o.store_id
                WHERE s.key = 'sodimac';

                CREATE TEMP TABLE sodimac_keep AS
                SELECT DISTINCT ON (store_id, canon_id, branch_code)
                       id AS keep_id, store_id, canon_id, branch_code, canon_url
                FROM sodimac_offers
                ORDER BY store_id, canon_id, branch_code, last_seen_at DESC, id DESC;

                CREATE TEMP TABLE sodimac_dups AS
                SELECT so.id AS dup_id, so.product_id, k.keep_id
                FROM sodimac_offers so
                JOIN sodimac_keep k USING (store_id, canon_id, branch_code)
                WHERE so.id <> k.keep_id;

                UPDATE price_points pp SET offer_id = d.keep_id
                FROM sodimac_dups d WHERE pp.offer_id = d.dup_id;

                DELETE FROM offers WHERE id IN (SELECT dup_id FROM sodimac_dups);

                UPDATE offers o SET external_id = left(k.canon_id, 200), url = left(k.canon_url, 1000)
                FROM sodimac_keep k WHERE o.id = k.keep_id;

                -- Productos que solo tenían la oferta duplicada, y totales de los afectados.
                DELETE FROM products p
                WHERE p.id IN (SELECT product_id FROM sodimac_dups)
                  AND NOT EXISTS (SELECT 1 FROM offers o WHERE o.product_id = p.id);

                UPDATE products p SET
                    offer_count = (SELECT count(DISTINCT o.store_id) FROM offers o WHERE o.product_id = p.id AND o.is_available),
                    best_price = (SELECT min(o.price) FROM offers o WHERE o.product_id = p.id AND o.is_available)
                WHERE p.id IN (SELECT product_id FROM sodimac_offers);

                DROP TABLE sodimac_dups;
                DROP TABLE sodimac_keep;
                DROP TABLE sodimac_offers;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seller",
                table: "offers");
        }
    }
}
