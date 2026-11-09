using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class StoreOwnedProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SellerOrders_OrderId_BrandId",
                table: "SellerOrders");

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "SellerSettlements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "SellerOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "OrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlements_StoreId",
                table: "SellerSettlements",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrders_OrderId_BrandId",
                table: "SellerOrders",
                columns: new[] { "OrderId", "BrandId" },
                unique: true,
                filter: "\"StoreId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrders_OrderId_StoreId_BrandId",
                table: "SellerOrders",
                columns: new[] { "OrderId", "StoreId", "BrandId" },
                unique: true,
                filter: "\"StoreId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrders_StoreId",
                table: "SellerOrders",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreId",
                table: "Products",
                column: "StoreId");

            migrationBuilder.Sql(StoreOwnedProductsBackfill.Sql);
            migrationBuilder.DropTable(name: "StoreProducts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_OwningStore",
                table: "Products",
                sql: "\"StoreId\" IS NOT NULL OR \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_StoreId",
                table: "OrderItems",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Stores_StoreId",
                table: "OrderItems",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Stores_StoreId",
                table: "Products",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerOrders_Stores_StoreId",
                table: "SellerOrders",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerSettlements_Stores_StoreId",
                table: "SellerSettlements",
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
                    IF EXISTS (SELECT 1 FROM "OrderItems" WHERE "StoreId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "SellerOrders" WHERE "StoreId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "SellerSettlements" WHERE "StoreId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot discard store snapshots after commerce activity; restore a reviewed backup instead';
                    END IF;
                END $$;
                CREATE TABLE "StoreProducts" (
                    "StoreId" uuid NOT NULL REFERENCES "Stores"("Id") ON DELETE NO ACTION,
                    "ProductId" uuid NOT NULL REFERENCES "Products"("Id") ON DELETE RESTRICT,
                    CONSTRAINT "PK_StoreProducts" PRIMARY KEY ("StoreId", "ProductId"));
                CREATE INDEX "IX_StoreProducts_ProductId_StoreId" ON "StoreProducts" ("ProductId", "StoreId");
                INSERT INTO "StoreProducts" SELECT "StoreId", "Id" FROM "Products" WHERE "StoreId" IS NOT NULL;
                """);
            foreach (var table in new[] { "OrderItems", "SellerOrders", "SellerSettlements", "Products" })
                migrationBuilder.DropForeignKey(name: $"FK_{table}_Stores_StoreId", table: table);
            migrationBuilder.DropCheckConstraint(name: "CK_Products_OwningStore", table: "Products");
            migrationBuilder.DropIndex(name: "IX_SellerOrders_OrderId_BrandId", table: "SellerOrders");
            migrationBuilder.DropIndex(name: "IX_SellerOrders_OrderId_StoreId_BrandId", table: "SellerOrders");
            foreach (var table in new[] { "OrderItems", "SellerOrders", "SellerSettlements", "Products" })
            {
                migrationBuilder.DropIndex(name: $"IX_{table}_StoreId", table: table);
                migrationBuilder.DropColumn(name: "StoreId", table: table);
            }
            migrationBuilder.CreateIndex(name: "IX_SellerOrders_OrderId_BrandId", table: "SellerOrders", columns: new[] { "OrderId", "BrandId" }, unique: true);
        }
    }
}
