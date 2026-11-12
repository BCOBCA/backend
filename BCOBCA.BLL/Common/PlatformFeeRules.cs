namespace BCOBCA.BLL.Common;

public static class PlatformFeeRules
{
    public static void Validate(decimal? rate)
    {
        if (rate is null || rate < 0 || rate > 1 || decimal.Round(rate.Value, 4) != rate.Value)
            throw new BusinessException("Tỷ lệ phí sàn phải từ 0 đến 1, tối đa 4 chữ số thập phân (0.05 = 5%).");
    }
}
