namespace BCOBCA.BLL.DTOs;

public sealed class AdminSupportQuery
{
    public string Search { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Status { get; set; } = "all";
    public string? From { get; set; }
    public string? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
public sealed record AdminSupportSummary(int Total, int Open, int Resolved);
public sealed record AdminSupportPage(IReadOnlyList<FeedbackDto> Items, int Page, int PageSize, int TotalItems,
    AdminSupportSummary Summary, IReadOnlyList<string> Subjects);
