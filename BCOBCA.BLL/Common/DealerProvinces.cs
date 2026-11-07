namespace BCOBCA.BLL.Common;

public static class DealerProvinces
{
    public const string Nationwide = "Toàn quốc";
    // Provincial catalog: https://datafiles.chinhphu.vn/cpp/files/vbpq/2025/7/19ttg.signed.pdf
    private static readonly (string Code, string Name)[] Catalog =
    [
        ("01", "Hà Nội"), ("04", "Cao Bằng"), ("08", "Tuyên Quang"), ("11", "Điện Biên"),
        ("12", "Lai Châu"), ("14", "Sơn La"), ("15", "Lào Cai"), ("19", "Thái Nguyên"),
        ("20", "Lạng Sơn"), ("22", "Quảng Ninh"), ("24", "Bắc Ninh"), ("25", "Phú Thọ"),
        ("31", "Hải Phòng"), ("33", "Hưng Yên"), ("37", "Ninh Bình"), ("38", "Thanh Hóa"),
        ("40", "Nghệ An"), ("42", "Hà Tĩnh"), ("44", "Quảng Trị"), ("46", "Huế"),
        ("48", "Đà Nẵng"), ("51", "Quảng Ngãi"), ("52", "Gia Lai"), ("56", "Khánh Hòa"),
        ("66", "Đắk Lắk"), ("68", "Lâm Đồng"), ("75", "Đồng Nai"), ("79", "Hồ Chí Minh"),
        ("80", "Tây Ninh"), ("82", "Đồng Tháp"), ("86", "Vĩnh Long"), ("91", "An Giang"),
        ("92", "Cần Thơ"), ("96", "Cà Mau")
    ];
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(Catalog.Select(x => x.Name).ToArray());

    public static string Resolve(string value) => Catalog.FirstOrDefault(x => x.Code == value || x.Name == value).Name
        ?? throw new BusinessException("Tỉnh/thành không hợp lệ", 400);

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1000)
            throw new BusinessException("Vui lòng chọn phạm vi hoạt động");
        if (value.Trim() == Nationwide) return Nationwide;
        var names = value.Split(',').Select(x => x.Trim()).ToArray();
        if (names.Any(x => !Names.Contains(x)))
            throw new BusinessException("Vui lòng chọn tỉnh/thành trong danh sách hoặc chọn Toàn quốc");
        return string.Join(", ", names.Distinct(StringComparer.Ordinal));
    }
}
