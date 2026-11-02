using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class GuardReviewRevisionAndShipmentIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_OrderId",
                table: "Shipments");

            migrationBuilder.AddColumn<Guid>(
                name: "Revision",
                table: "ProductReviews",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ProductReviews",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId_Carrier_TrackingCode",
                table: "Shipments",
                columns: new[] { "OrderId", "Carrier", "TrackingCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_OrderId_Carrier_TrackingCode",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ProductReviews");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId",
                table: "Shipments",
                column: "OrderId");
        }
    }
}
