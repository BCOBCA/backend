namespace BCOBCA.BLL.Common;

public static class GhnLifecycleRules
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string> {
        ["ready_to_pick"] = "Chờ GHN lấy hàng", ["picking"] = "GHN đang đến lấy hàng",
        ["money_collect_picking"] = "GHN đang thu tiền khi lấy hàng", ["picked"] = "GHN đã lấy hàng",
        ["storing"] = "Hàng đang ở kho GHN", ["sorting"] = "GHN đang phân loại hàng",
        ["transporting"] = "GHN đang vận chuyển", ["delivering"] = "GHN đang giao hàng cho bạn",
        ["money_collect_delivering"] = "GHN đang thu tiền khi giao hàng", ["delivered"] = "Giao hàng thành công",
        ["delivery_fail"] = "Giao hàng chưa thành công", ["waiting_to_return"] = "Đơn hàng đang chờ hoàn về shop",
        ["return"] = "Đã yêu cầu hoàn hàng", ["return_transporting"] = "Đang vận chuyển hàng hoàn",
        ["return_sorting"] = "Đang phân loại hàng hoàn", ["returning"] = "GHN đang hoàn hàng về shop",
        ["return_fail"] = "Hoàn hàng chưa thành công", ["returned"] = "Đã hoàn hàng về shop",
        ["cancel"] = "Vận đơn GHN đã hủy", ["exception"] = "Vận đơn gặp sự cố cần xử lý",
        ["lost"] = "GHN báo thất lạc hàng", ["damage"] = "GHN báo hàng hư hỏng", ["scrap"] = "GHN báo hàng đã tiêu hủy",
    };
    public static string Label(string status) => Labels.TryGetValue(status, out var label) ? label : throw new BusinessException("Trạng thái GHN chưa được hỗ trợ", 400);
    public static bool IsTerminal(string status) => status is "delivered" or "returned" or "cancel" or "exception" or "lost" or "damage" or "scrap";
    public static bool HasPickedUp(string status) => status is "picked" or "storing" or "sorting" or "transporting" or "delivering" or "money_collect_delivering" or "delivered" or "delivery_fail" or "waiting_to_return" or "return" or "return_transporting" or "return_sorting" or "returning" or "return_fail" or "returned";
}
