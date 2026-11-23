using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerVoucherManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vouchers_StoreId",
                table: "Vouchers");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Vouchers",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                table: "Vouchers",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DisabledByAccountId",
                table: "Vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabledReason",
                table: "Vouchers",
                type: "varchar(1000)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Vouchers",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_DisabledByAccountId",
                table: "Vouchers",
                column: "DisabledByAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_StoreId_StartsAt_ExpiresAt",
                table: "Vouchers",
                columns: new[] { "StoreId", "StartsAt", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Accounts_DisabledByAccountId",
                table: "Vouchers",
                column: "DisabledByAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Accounts_DisabledByAccountId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_DisabledByAccountId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_StoreId_StartsAt_ExpiresAt",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "DisabledByAccountId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "DisabledReason",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_StoreId",
                table: "Vouchers",
                column: "StoreId");
        }
    }
}
