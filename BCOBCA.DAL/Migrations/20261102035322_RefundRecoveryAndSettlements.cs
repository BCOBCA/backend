using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RefundRecoveryAndSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AffiliateWalletTransactions_Balance",
                table: "AffiliateWalletTransactions");

            migrationBuilder.AddColumn<long>(
                name: "PointDebt",
                table: "UserMemberships",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "SellerSettlements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TransactionReference",
                table: "SellerSettlements",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "SellerSettlements",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Refunds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "Refunds",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RestockReturnedItems",
                table: "Refunds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Refunds",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<decimal>(
                name: "DebtBalance",
                table: "AffiliateWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SellerSettlementAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerSettlementAdjustments", x => x.Id);
                    table.CheckConstraint("CK_SettlementAdjustment_Amounts", "\"Amount\" > 0 AND \"RemainingAmount\" BETWEEN 0 AND \"Amount\"");
                    table.ForeignKey(
                        name: "FK_SellerSettlementAdjustments_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerSettlementAdjustments_SellerSettlements_SellerSettlem~",
                        column: x => x.SellerSettlementId,
                        principalTable: "SellerSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellerSettlementRecoveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedToSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerSettlementRecoveries", x => x.Id);
                    table.CheckConstraint("CK_SettlementRecovery_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_SellerSettlementRecoveries_SellerSettlementAdjustments_Adju~",
                        column: x => x.AdjustmentId,
                        principalTable: "SellerSettlementAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SellerSettlementRecoveries_SellerSettlements_AppliedToSettl~",
                        column: x => x.AppliedToSettlementId,
                        principalTable: "SellerSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserMemberships_PointDebt",
                table: "UserMemberships",
                sql: "\"PointDebt\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlements_TransactionReference",
                table: "SellerSettlements",
                column: "TransactionReference",
                unique: true,
                filter: "\"TransactionReference\" <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SellerSettlements_Amounts",
                table: "SellerSettlements",
                sql: "\"GrossAmount\" >= 0 AND \"FeeAmount\" >= 0 AND \"NetAmount\" >= 0 AND \"PaidAmount\" BETWEEN 0 AND \"NetAmount\"");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId",
                unique: true,
                filter: "\"Status\" IN ('PENDING','APPROVED','COMPLETED')");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId_IdempotencyKey",
                table: "Refunds",
                columns: new[] { "PaymentId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ProviderRefundCode",
                table: "Refunds",
                column: "ProviderRefundCode",
                unique: true,
                filter: "\"ProviderRefundCode\" <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Refunds_Amount",
                table: "Refunds",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AffiliateWalletTransactions_Balance",
                table: "AffiliateWalletTransactions",
                sql: "\"BalanceAfter\" >= 0 AND ((\"Type\" IN ('CREDIT','PENDING_CREDIT','DEBT_CREDIT') AND \"Amount\" > 0) OR (\"Type\" IN ('DEBIT','PENDING_DEBIT','DEBT_DEBIT','RECOVERY_DEBIT') AND \"Amount\" < 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AffiliateWallets_Debt",
                table: "AffiliateWallets",
                sql: "\"DebtBalance\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlementAdjustments_RefundId",
                table: "SellerSettlementAdjustments",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlementAdjustments_SellerSettlementId_RefundId",
                table: "SellerSettlementAdjustments",
                columns: new[] { "SellerSettlementId", "RefundId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlementRecoveries_AdjustmentId_AppliedToSettlement~",
                table: "SellerSettlementRecoveries",
                columns: new[] { "AdjustmentId", "AppliedToSettlementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerSettlementRecoveries_AppliedToSettlementId",
                table: "SellerSettlementRecoveries",
                column: "AppliedToSettlementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SellerSettlementRecoveries");

            migrationBuilder.DropTable(
                name: "SellerSettlementAdjustments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserMemberships_PointDebt",
                table: "UserMemberships");

            migrationBuilder.DropIndex(
                name: "IX_SellerSettlements_TransactionReference",
                table: "SellerSettlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SellerSettlements_Amounts",
                table: "SellerSettlements");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_PaymentId_IdempotencyKey",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_ProviderRefundCode",
                table: "Refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Refunds_Amount",
                table: "Refunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AffiliateWalletTransactions_Balance",
                table: "AffiliateWalletTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AffiliateWallets_Debt",
                table: "AffiliateWallets");

            migrationBuilder.DropColumn(
                name: "PointDebt",
                table: "UserMemberships");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "SellerSettlements");

            migrationBuilder.DropColumn(
                name: "TransactionReference",
                table: "SellerSettlements");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "SellerSettlements");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "RestockReturnedItems",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "DebtBalance",
                table: "AffiliateWallets");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AffiliateWalletTransactions_Balance",
                table: "AffiliateWalletTransactions",
                sql: "\"BalanceAfter\" >= 0 AND ((\"Type\" IN ('CREDIT','PENDING_CREDIT') AND \"Amount\" > 0) OR (\"Type\" IN ('DEBIT','PENDING_DEBIT') AND \"Amount\" < 0))");
        }
    }
}
