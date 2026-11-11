using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class StoreVoucherAndGhnTestShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "Vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GhnTestShopId",
                table: "Stores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingHeightCm",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingLengthCm",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingWeightGrams",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingWidthCm",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_StoreId",
                table: "Vouchers",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Stores_StoreId",
                table: "Vouchers",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Vouchers" WHERE "StoreId" IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM "Orders" WHERE "CheckoutGroupId" IS NOT NULL GROUP BY "CheckoutGroupId" HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Cannot remove seller voucher scope or shipping metadata after multi-store checkout is in use.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Stores_StoreId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_StoreId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "GhnTestShopId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "ShippingHeightCm",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShippingLengthCm",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShippingWeightGrams",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShippingWidthCm",
                table: "Products");
        }
    }
}
