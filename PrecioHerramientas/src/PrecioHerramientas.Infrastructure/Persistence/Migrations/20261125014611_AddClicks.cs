using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PrecioHerramientas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clicks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    offer_id = table.Column<int>(type: "integer", nullable: true),
                    product_id = table.Column<int>(type: "integer", nullable: true),
                    store_id = table.Column<int>(type: "integer", nullable: false),
                    clicked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    anonymized_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    referer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clicks", x => x.id);
                    table.ForeignKey(
                        name: "fk_clicks_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_clicks_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_clicks_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clicks_clicked_at",
                table: "clicks",
                column: "clicked_at");

            migrationBuilder.CreateIndex(
                name: "ix_clicks_offer_id",
                table: "clicks",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "ix_clicks_product_id_clicked_at",
                table: "clicks",
                columns: new[] { "product_id", "clicked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_clicks_store_id_clicked_at",
                table: "clicks",
                columns: new[] { "store_id", "clicked_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clicks");
        }
    }
}
