using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrecioHerramientas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_offers_store_id_external_id",
                table: "offers");

            migrationBuilder.AddColumn<string>(
                name: "branch_code",
                table: "offers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "branch_name",
                table: "offers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_offers_store_id_external_id_branch_code",
                table: "offers",
                columns: new[] { "store_id", "external_id", "branch_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_offers_store_id_external_id_branch_code",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "branch_code",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "branch_name",
                table: "offers");

            migrationBuilder.CreateIndex(
                name: "ix_offers_store_id_external_id",
                table: "offers",
                columns: new[] { "store_id", "external_id" },
                unique: true);
        }
    }
}
