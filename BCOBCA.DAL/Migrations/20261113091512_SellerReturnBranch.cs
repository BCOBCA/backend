using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class SellerReturnBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "StoreBranches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_StoreBranches_Return",
                table: "StoreBranches",
                column: "StoreId",
                unique: true,
                filter: "\"IsReturn\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoreBranches_Return",
                table: "StoreBranches",
                sql: "NOT \"IsReturn\" OR (\"IsActive\" AND \"Address\" <> '' AND \"Phone\" <> '' AND \"ProvinceName\" <> '' AND \"WardName\" <> '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoreBranches_Return",
                table: "StoreBranches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StoreBranches_Return",
                table: "StoreBranches");

            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "StoreBranches");
        }
    }
}
