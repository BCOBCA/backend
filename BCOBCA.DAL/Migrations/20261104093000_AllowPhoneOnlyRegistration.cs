using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    public partial class AllowPhoneOnlyRegistration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Accounts_Email", table: "Accounts");
            migrationBuilder.CreateIndex(name: "IX_Accounts_Email", table: "Accounts", column: "Email",
                unique: true, filter: "\"Email\" <> ''");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Accounts_Email", table: "Accounts");
            migrationBuilder.CreateIndex(name: "IX_Accounts_Email", table: "Accounts", column: "Email", unique: true);
        }
    }
}
