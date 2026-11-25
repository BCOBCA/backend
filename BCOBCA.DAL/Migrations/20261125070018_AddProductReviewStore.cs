using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddProductReviewStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "ProductReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ProductReviews" AS review
                SET "StoreId" = COALESCE(item."StoreId", product."StoreId")
                FROM "OrderItems" AS item, "Products" AS product
                WHERE review."OrderItemId" = item."Id"
                  AND review."ProductId" = product."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_StoreId_Status",
                table: "ProductReviews",
                columns: new[] { "StoreId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReviews_Stores_StoreId",
                table: "ProductReviews",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductReviews_Stores_StoreId",
                table: "ProductReviews");

            migrationBuilder.DropIndex(
                name: "IX_ProductReviews_StoreId_Status",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "ProductReviews");
        }
    }
}
