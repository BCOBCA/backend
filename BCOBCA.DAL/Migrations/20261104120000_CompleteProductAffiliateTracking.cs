using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class CompleteProductAffiliateTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AffiliateBudget",
                table: "Products",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AffiliateEnabled",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AffiliateMaxRate",
                table: "Products",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "AffiliateCommissions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Rank",
                table: "AffiliateCommissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RankName",
                table: "AffiliateCommissions",
                type: "varchar(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "AffiliateCommissions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "AffiliateVisits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenHash = table.Column<string>(type: "varchar(64)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateVisits_AffiliateLinks_AffiliateLinkId",
                        column: x => x.AffiliateLinkId,
                        principalTable: "AffiliateLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AffiliateVisits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Update previous defaults while retaining policies already customized by admin.
            migrationBuilder.Sql("UPDATE \"AffiliateRanks\" SET \"MinSuccessfulReferrals\" = 60 WHERE \"Id\" = 'a704b6f3-5664-4428-93d1-668522026204' AND \"MinSuccessfulReferrals\" = 70;");
            migrationBuilder.Sql("UPDATE \"AffiliateRanks\" SET \"MinSuccessfulReferrals\" = 100 WHERE \"Id\" = 'a704b6f3-5664-4428-93d1-668522026205' AND \"MinSuccessfulReferrals\" = 150;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_AffiliatePolicy",
                table: "Products",
                sql: "(\"AffiliateMaxRate\" IS NULL OR \"AffiliateMaxRate\" BETWEEN 0 AND 1) AND (\"AffiliateBudget\" IS NULL OR \"AffiliateBudget\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AffiliateCommissions_Paid",
                table: "AffiliateCommissions",
                sql: "\"PaidAmount\" >= 0 AND \"PaidAmount\" <= \"CommissionAmount\"");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateVisits_AffiliateLinkId_CreatedAt",
                table: "AffiliateVisits",
                columns: new[] { "AffiliateLinkId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateVisits_ExpiresAt",
                table: "AffiliateVisits",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateVisits_TokenHash",
                table: "AffiliateVisits",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateVisits_UserId",
                table: "AffiliateVisits",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateVisits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_AffiliatePolicy",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AffiliateCommissions_Paid",
                table: "AffiliateCommissions");

            migrationBuilder.DropColumn(
                name: "AffiliateBudget",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AffiliateEnabled",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AffiliateMaxRate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "AffiliateCommissions");

            migrationBuilder.DropColumn(
                name: "Rank",
                table: "AffiliateCommissions");

            migrationBuilder.DropColumn(
                name: "RankName",
                table: "AffiliateCommissions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "AffiliateCommissions");

            migrationBuilder.Sql("UPDATE \"AffiliateRanks\" SET \"MinSuccessfulReferrals\" = 70 WHERE \"Id\" = 'a704b6f3-5664-4428-93d1-668522026204' AND \"MinSuccessfulReferrals\" = 60;");
            migrationBuilder.Sql("UPDATE \"AffiliateRanks\" SET \"MinSuccessfulReferrals\" = 150 WHERE \"Id\" = 'a704b6f3-5664-4428-93d1-668522026205' AND \"MinSuccessfulReferrals\" = 100;");
        }
    }
}
