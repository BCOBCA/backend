namespace BCOBCA.BLL.Common;

public sealed record ApiResponse<T>(bool Success, string Message, T? Data, object? Errors = null)
{
    public static ApiResponse<T> Ok(T data, string message = "Thành công") => new(true, message, data);
}

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
}
