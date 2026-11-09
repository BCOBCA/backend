using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Specifications",
                table: "Products",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "AttributeDefinitions",
                table: "Categories",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Specifications_Object",
                table: "Products",
                sql: "jsonb_typeof(\"Specifications\") = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Categories_AttributeDefinitions_Array",
                table: "Categories",
                sql: "jsonb_typeof(\"AttributeDefinitions\") = 'array'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Specifications_Object",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Categories_AttributeDefinitions_Array",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Specifications",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AttributeDefinitions",
                table: "Categories");
        }
    }
}
