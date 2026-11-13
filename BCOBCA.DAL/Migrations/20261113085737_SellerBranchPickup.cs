using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class SellerBranchPickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPickup",
                table: "StoreBranches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceName",
                table: "StoreBranches",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WardName",
                table: "StoreBranches",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_StoreBranches_Pickup",
                table: "StoreBranches",
                column: "StoreId",
                unique: true,
                filter: "\"IsPickup\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoreBranches_Pickup",
                table: "StoreBranches",
                sql: "NOT \"IsPickup\" OR (\"IsActive\" AND \"Address\" <> '' AND \"Phone\" <> '' AND \"ProvinceName\" <> '' AND \"WardName\" <> '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoreBranches_Pickup",
                table: "StoreBranches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StoreBranches_Pickup",
                table: "StoreBranches");

            migrationBuilder.DropColumn(
                name: "IsPickup",
                table: "StoreBranches");

            migrationBuilder.DropColumn(
                name: "ProvinceName",
                table: "StoreBranches");

            migrationBuilder.DropColumn(
                name: "WardName",
                table: "StoreBranches");
        }
    }
}
