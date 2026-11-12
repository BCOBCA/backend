using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AffiliateWithdrawalFeesAndBanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                table: "AffiliateWithdrawals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "AffiliateWithdrawals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AffiliateBankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MaskedAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DetailsEncrypted = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateBankAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    UpdatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalConfigurations", x => x.Id);
                    table.CheckConstraint("CK_WithdrawalConfigurations_Amounts", "\"FeeAmount\" >= 0 AND \"MinimumAmount\" > \"FeeAmount\" AND \"MaximumAmount\" >= \"MinimumAmount\"");
                    table.CheckConstraint("CK_WithdrawalConfigurations_Singleton", "\"Id\" = 'ca401bc0-b07b-4eb4-9b1d-30e229fdc943'::uuid");
                    table.ForeignKey(
                        name: "FK_WithdrawalConfigurations_Accounts_UpdatedByAccountId",
                        column: x => x.UpdatedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "WithdrawalConfigurations",
                columns: new[] { "Id", "FeeAmount", "MaximumAmount", "MinimumAmount", "Revision", "UpdatedAt", "UpdatedByAccountId" },
                values: new object[] { new Guid("ca401bc0-b07b-4eb4-9b1d-30e229fdc943"), 0m, 50000000m, 50000m, new Guid("c50e4db3-46b0-42de-a979-c2108fb1c599"), null, null });

            // Existing requests were created without fees; preserve their full payout.
            migrationBuilder.Sql("UPDATE \"AffiliateWithdrawals\" SET \"NetAmount\" = \"Amount\";");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AffiliateWithdrawals_Fee",
                table: "AffiliateWithdrawals",
                sql: "\"FeeAmount\" >= 0 AND \"NetAmount\" > 0 AND \"NetAmount\" = \"Amount\" - \"FeeAmount\"");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateBankAccounts_UserId_CreatedAt",
                table: "AffiliateBankAccounts",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalConfigurations_UpdatedByAccountId",
                table: "WithdrawalConfigurations",
                column: "UpdatedByAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateBankAccounts");

            migrationBuilder.DropTable(
                name: "WithdrawalConfigurations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AffiliateWithdrawals_Fee",
                table: "AffiliateWithdrawals");

            migrationBuilder.DropColumn(
                name: "FeeAmount",
                table: "AffiliateWithdrawals");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "AffiliateWithdrawals");
        }
    }
}
