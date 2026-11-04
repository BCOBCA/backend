using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherRedemptionOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFlashSale",
                table: "Vouchers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinLevel",
                table: "Vouchers",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "PointCost",
                table: "Vouchers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vouchers_Membership",
                table: "Vouchers",
                sql: "\"PointCost\" >= 0 AND \"MinLevel\" BETWEEN 1 AND 10 AND (\"PointCost\" = 0 OR \"PerUserLimit\" = 1)");

            // Finite, dated offers. Do not replace codes or offers already configured by admin.
            migrationBuilder.Sql("""
                INSERT INTO "Vouchers" ("Id", "Code", "Name", "Description", "DiscountType", "DiscountValue", "MaxDiscount", "MinOrderAmount", "UsageLimit", "PerUserLimit", "StartsAt", "ExpiresAt", "Status", "PointCost", "MinLevel", "IsFlashSale") VALUES
                ('da704650-8561-4812-9a20-668522026301', 'BCA20K', 'Giảm 20.000đ', 'Giảm 20.000đ cho đơn hàng từ 200.000đ. Mỗi thành viên sử dụng một lần.', 'FIXED', 20000, NULL, 200000, 1000, 1, CURRENT_TIMESTAMP - INTERVAL '1 minute', CURRENT_TIMESTAMP + INTERVAL '90 days', 'ACTIVE', 0, 1, FALSE),
                ('da704650-8561-4812-9a20-668522026302', 'BCA10PHANTRAM', 'Giảm 10%', 'Giảm 10%, tối đa 50.000đ cho đơn hàng từ 200.000đ.', 'PERCENT', 10, 50000, 200000, 1000, 1, CURRENT_TIMESTAMP - INTERVAL '1 minute', CURRENT_TIMESTAMP + INTERVAL '90 days', 'ACTIVE', 0, 1, FALSE),
                ('da704650-8561-4812-9a20-668522026303', 'BCALV350K', 'Giảm 50.000đ', 'Đặc quyền từ Lv.3: giảm 50.000đ cho đơn hàng từ 500.000đ.', 'FIXED', 50000, NULL, 500000, 1000, 1, CURRENT_TIMESTAMP - INTERVAL '1 minute', CURRENT_TIMESTAMP + INTERVAL '90 days', 'MEMBER_OFFER', 0, 3, FALSE),
                ('da704650-8561-4812-9a20-668522026304', 'BCADOI30K', 'Voucher giảm 30.000đ', 'Đổi 500 điểm lấy voucher giảm 30.000đ cho đơn từ 200.000đ. Mỗi tài khoản đổi một lần.', 'FIXED', 30000, NULL, 200000, 500, 1, CURRENT_TIMESTAMP - INTERVAL '1 minute', CURRENT_TIMESTAMP + INTERVAL '90 days', 'MEMBER_OFFER', 500, 1, FALSE),
                ('da704650-8561-4812-9a20-668522026305', 'BCADOI50K', 'Voucher giảm 50.000đ', 'Đổi 800 điểm lấy voucher giảm 50.000đ cho đơn từ 300.000đ. Mỗi tài khoản đổi một lần.', 'FIXED', 50000, NULL, 300000, 500, 1, CURRENT_TIMESTAMP - INTERVAL '1 minute', CURRENT_TIMESTAMP + INTERVAL '90 days', 'MEMBER_OFFER', 800, 1, FALSE),
                ('da704650-8561-4812-9a20-668522026306', 'BCAFLASH15', 'Giảm nhanh 15%', 'Ưu đãi có hạn: giảm 15%, tối đa 30.000đ cho đơn từ 150.000đ.', 'PERCENT', 15, 30000, 150000, 100, 1, CURRENT_TIMESTAMP - INTERVAL '1 minute', CURRENT_TIMESTAMP + INTERVAL '3 days', 'ACTIVE', 0, 1, TRUE)
                ON CONFLICT ("Code") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve claim/order history, but stop issuing these offers on rollback.
            migrationBuilder.Sql("UPDATE \"Vouchers\" SET \"Status\" = 'INACTIVE' WHERE \"Id\" IN ('da704650-8561-4812-9a20-668522026301', 'da704650-8561-4812-9a20-668522026302', 'da704650-8561-4812-9a20-668522026303', 'da704650-8561-4812-9a20-668522026304', 'da704650-8561-4812-9a20-668522026305', 'da704650-8561-4812-9a20-668522026306');");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vouchers_Membership",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "IsFlashSale",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "MinLevel",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "PointCost",
                table: "Vouchers");
        }
    }
}
