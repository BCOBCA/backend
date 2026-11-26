using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class UseSingleAccountRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old access tokens contain claims sourced from AccountRoles. Invalidate
            // their families before switching to Accounts.Role as the sole authority.
            migrationBuilder.Sql("UPDATE \"RefreshTokens\" SET \"RevokedAt\" = CURRENT_TIMESTAMP WHERE \"RevokedAt\" IS NULL;");
            migrationBuilder.DropTable(
                name: "AccountRoles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "varchar(30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRoles_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRoles_AccountId_Role",
                table: "AccountRoles",
                columns: new[] { "AccountId", "Role" },
                unique: true);

            // Restore a usable legacy model on rollback. Original multi-role rows
            // must be restored from the pre-deployment backup if they are needed.
            migrationBuilder.Sql("INSERT INTO \"AccountRoles\" (\"Id\", \"AccountId\", \"Role\") SELECT gen_random_uuid(), \"Id\", \"Role\" FROM \"Accounts\" WHERE \"Role\" <> '';");
        }
    }
}
