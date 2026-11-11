using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class SplitCheckoutByStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutGroupId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_CheckoutGroupId",
                table: "Orders",
                columns: new[] { "UserId", "CheckoutGroupId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Orders" WHERE "CheckoutGroupId" IS NOT NULL GROUP BY "CheckoutGroupId" HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Cannot remove checkout groups after multi-store orders have been created.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_CheckoutGroupId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CheckoutGroupId",
                table: "Orders");
        }
    }
}
