namespace BCOBCA.BLL.Common;

public sealed class BusinessException(string message, int statusCode = 422) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
