using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BCOBCA.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippingDiscountAmount",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_ShippingDiscount",
                table: "Orders",
                sql: "\"ShippingDiscountAmount\" >= 0 AND \"ShippingDiscountAmount\" <= \"ShippingFee\"");

            migrationBuilder.Sql("""
                INSERT INTO "Vouchers" ("Id", "Code", "Name", "Description", "DiscountType", "DiscountValue", "MaxDiscount", "MinOrderAmount", "UsageLimit", "PerUserLimit", "StartsAt", "ExpiresAt", "Status", "PointCost", "MinLevel", "IsFlashSale") VALUES
                ('da704650-8561-4812-9a20-668522026307', 'BCASHIP30K', 'Giảm ship 30.000đ', 'Giảm phí vận chuyển tối đa 30.000đ cho đơn hàng từ 300.000đ. Không quy đổi thành tiền, mỗi tài khoản sử dụng một lần.', 'SHIPPING', 30000, 30000, 300000, 1000, 1, CURRENT_TIMESTAMP - INTERVAL '1 minute', CURRENT_TIMESTAMP + INTERVAL '90 days', 'MEMBER_OFFER', 0, 1, FALSE),
                ('da704650-8561-4812-9a20-668522026308', 'BCADOISHIP30K', 'Voucher phí vận chuyển', 'Đổi 1.000 điểm lấy voucher giảm phí vận chuyển tối đa 30.000đ cho đơn từ 300.000đ. Mỗi tài khoản đổi và sử dụng một lần.', 'SHIPPING', 30000, 30000, 300000, 500, 1, CURRENT_TIMESTAMP - INTERVAL '1 minute', CURRENT_TIMESTAMP + INTERVAL '90 days', 'MEMBER_OFFER', 1000, 1, FALSE)
                ON CONFLICT ("Code") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Vouchers\" SET \"Status\" = 'INACTIVE' WHERE \"Id\" IN ('da704650-8561-4812-9a20-668522026307', 'da704650-8561-4812-9a20-668522026308');");
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_ShippingDiscount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingDiscountAmount",
                table: "Orders");
        }
    }
}
