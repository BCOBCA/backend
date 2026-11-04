using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipLevelsAndAffiliateRanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AffiliateRanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", nullable: false),
                    MinSuccessfulReferrals = table.Column<int>(type: "int", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateRanks", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AffiliateRanks",
                columns: new[] { "Id", "CommissionRate", "MinSuccessfulReferrals", "Name", "Rank" },
                values: new object[,]
                {
                    { new Guid("a704b6f3-5664-4428-93d1-668522026201"), 0.02m, 0, "Đồng", 1 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026202"), 0.03m, 10, "Bạc", 2 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026203"), 0.05m, 30, "Vàng", 3 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026204"), 0.07m, 70, "Bạch kim", 4 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026205"), 0.10m, 150, "Kim cương", 5 }
                });

            migrationBuilder.UpdateData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026001"),
                columns: new[] { "Benefits", "Name" },
                values: new object[] { "{\"items\":[]}", "Lv.1" });

            migrationBuilder.InsertData(
                table: "MembershipTiers",
                columns: new[] { "Id", "Benefits", "IsActive", "MinPoints", "MinSpend", "Name", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("a704b6f3-5664-4428-93d1-668522026101"), "{\"items\":[]}", true, 100, 0m, "Lv.2", 1 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026102"), "{\"items\":[]}", true, 300, 0m, "Lv.3", 2 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026103"), "{\"items\":[]}", true, 600, 0m, "Lv.4", 3 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026104"), "{\"items\":[]}", true, 1000, 0m, "Lv.5", 4 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026105"), "{\"items\":[]}", true, 1600, 0m, "Lv.6", 5 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026106"), "{\"items\":[]}", true, 2500, 0m, "Lv.7", 6 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026107"), "{\"items\":[]}", true, 4000, 0m, "Lv.8", 7 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026108"), "{\"items\":[]}", true, 6000, 0m, "Lv.9", 8 },
                    { new Guid("a704b6f3-5664-4428-93d1-668522026109"), "{\"items\":[]}", true, 10000, 0m, "Lv.10", 9 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateRanks_Rank",
                table: "AffiliateRanks",
                column: "Rank",
                unique: true);

            migrationBuilder.Sql("""
                UPDATE "UserMemberships" AS membership
                SET "TierId" = (
                    SELECT tier."Id" FROM "MembershipTiers" AS tier
                    WHERE tier."IsActive" AND tier."MinPoints" <= membership."AvailablePoints"
                    ORDER BY tier."SortOrder" DESC LIMIT 1
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserMemberships"
                SET "TierId" = 'a704b6f3-5664-4428-93d1-668522026001'::uuid
                WHERE "TierId" IN (
                    'a704b6f3-5664-4428-93d1-668522026101'::uuid, 'a704b6f3-5664-4428-93d1-668522026102'::uuid,
                    'a704b6f3-5664-4428-93d1-668522026103'::uuid, 'a704b6f3-5664-4428-93d1-668522026104'::uuid,
                    'a704b6f3-5664-4428-93d1-668522026105'::uuid, 'a704b6f3-5664-4428-93d1-668522026106'::uuid,
                    'a704b6f3-5664-4428-93d1-668522026107'::uuid, 'a704b6f3-5664-4428-93d1-668522026108'::uuid,
                    'a704b6f3-5664-4428-93d1-668522026109'::uuid
                );
                """);

            migrationBuilder.DropTable(
                name: "AffiliateRanks");

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026101"));

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026102"));

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026103"));

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026104"));

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026105"));

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026106"));

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026107"));

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026108"));

            migrationBuilder.DeleteData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026109"));

            migrationBuilder.UpdateData(
                table: "MembershipTiers",
                keyColumn: "Id",
                keyValue: new Guid("a704b6f3-5664-4428-93d1-668522026001"),
                columns: new[] { "Benefits", "Name" },
                values: new object[] { "{}", "Thành viên" });
        }
    }
}
