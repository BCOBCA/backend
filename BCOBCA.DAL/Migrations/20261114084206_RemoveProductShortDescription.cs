using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductShortDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Products"
                SET "Description" = "ShortDescription"
                WHERE btrim("Description", E' \t\n\r') = ''
                  AND btrim("ShortDescription", E' \t\n\r') <> '';
                """);
            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
