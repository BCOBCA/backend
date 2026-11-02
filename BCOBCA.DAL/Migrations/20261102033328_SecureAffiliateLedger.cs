using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class SecureAffiliateLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_MembershipId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateWithdrawals_WalletId",
                table: "AffiliateWithdrawals");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateWalletTransactions_WalletId",
                table: "AffiliateWalletTransactions");

            migrationBuilder.AddColumn<string>(
                name: "BankDetailsEncrypted",
                table: "AffiliateWithdrawals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AffiliateWithdrawals",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "AffiliateWithdrawals",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_MembershipId_Type_ReferenceType_Reference~",
                table: "PointTransactions",
                columns: new[] { "MembershipId", "Type", "ReferenceType", "ReferenceId" },
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateWithdrawals_TransactionReference",
                table: "AffiliateWithdrawals",
                column: "TransactionReference",
                unique: true,
                filter: "\"TransactionReference\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateWithdrawals_WalletId_IdempotencyKey",
                table: "AffiliateWithdrawals",
                columns: new[] { "WalletId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateWalletTransactions_WalletId_Type_ReferenceType_Ref~",
                table: "AffiliateWalletTransactions",
                columns: new[] { "WalletId", "Type", "ReferenceType", "ReferenceId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AffiliateWalletTransactions_Balance",
                table: "AffiliateWalletTransactions",
                sql: "\"BalanceAfter\" >= 0 AND ((\"Type\" IN ('CREDIT','PENDING_CREDIT') AND \"Amount\" > 0) OR (\"Type\" IN ('DEBIT','PENDING_DEBIT') AND \"Amount\" < 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AffiliateCommissions_Amounts",
                table: "AffiliateCommissions",
                sql: "\"CommissionRate\" BETWEEN 0 AND 1 AND \"BaseAmount\" >= 0 AND \"CommissionAmount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_MembershipId_Type_ReferenceType_Reference~",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateWithdrawals_TransactionReference",
                table: "AffiliateWithdrawals");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateWithdrawals_WalletId_IdempotencyKey",
                table: "AffiliateWithdrawals");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateWalletTransactions_WalletId_Type_ReferenceType_Ref~",
                table: "AffiliateWalletTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AffiliateWalletTransactions_Balance",
                table: "AffiliateWalletTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AffiliateCommissions_Amounts",
                table: "AffiliateCommissions");

            migrationBuilder.DropColumn(
                name: "BankDetailsEncrypted",
                table: "AffiliateWithdrawals");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AffiliateWithdrawals");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "AffiliateWithdrawals");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_MembershipId",
                table: "PointTransactions",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateWithdrawals_WalletId",
                table: "AffiliateWithdrawals",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateWalletTransactions_WalletId",
                table: "AffiliateWalletTransactions",
                column: "WalletId");
        }
    }
}
