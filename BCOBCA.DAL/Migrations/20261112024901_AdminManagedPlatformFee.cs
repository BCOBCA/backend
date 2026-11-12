using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AdminManagedPlatformFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformFeeConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformFeeRate = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false, defaultValue: 0m),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    UpdatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformFeeConfigurations", x => x.Id);
                    table.CheckConstraint("CK_PlatformFeeConfigurations_Rate", "\"PlatformFeeRate\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_PlatformFeeConfigurations_Singleton", "\"Id\" = 'e312fc37-3690-41a2-80dd-8f0276adf492'::uuid");
                    table.ForeignKey(
                        name: "FK_PlatformFeeConfigurations_Accounts_UpdatedByAccountId",
                        column: x => x.UpdatedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "PlatformFeeConfigurations",
                columns: new[] { "Id", "Revision", "UpdatedAt", "UpdatedByAccountId" },
                values: new object[] { new Guid("e312fc37-3690-41a2-80dd-8f0276adf492"), new Guid("07e3ef2e-844e-49b1-b0ef-822eb4f17483"), null, null });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformFeeConfigurations_UpdatedByAccountId",
                table: "PlatformFeeConfigurations",
                column: "UpdatedByAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformFeeConfigurations");
        }
    }
}
