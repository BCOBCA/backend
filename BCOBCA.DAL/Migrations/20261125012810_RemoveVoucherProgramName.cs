using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVoucherProgramName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Vouchers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Vouchers",
                type: "varchar(150)",
                nullable: false,
                defaultValue: "");
        }
    }
}
