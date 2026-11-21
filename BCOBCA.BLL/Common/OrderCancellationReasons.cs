namespace BCOBCA.BLL.Common;

public static class OrderCancellationReasons
{
    public static readonly IReadOnlyList<string> BuyerChoices = Array.AsReadOnly(new[] {
        "Không còn nhu cầu mua", "Đặt nhầm hoặc trùng đơn", "Muốn thay đổi sản phẩm",
        "Muốn thay đổi địa chỉ nhận hàng", "Tìm được giá tốt hơn",
    });

    public static string Buyer(string? reason)
    {
        var value = reason?.Trim();
        if (value is null || !BuyerChoices.Contains(value))
            throw new BusinessException("Vui lòng chọn lý do hủy đơn trong danh sách.", 400);
        return value;
    }

    public static string Seller(string? reason)
    {
        var value = reason?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 500)
            throw new BusinessException("Vui lòng nhập lý do hủy đơn từ 1 đến 500 ký tự.", 400);
        return value;
    }
}
