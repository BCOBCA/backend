using BCOBCA.BLL.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BCOBCA.API.Middlewares;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;
            var (status, message, errors) = ex switch
            {
                BusinessException business => (business.StatusCode, business.Message, (object?)null),
                ValidationException validation => (422, "Dữ liệu không hợp lệ", (object?)validation.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray())),
                Exception conflict when DatabaseConflicts.IsConcurrency(conflict) => (409, "Dữ liệu vừa thay đổi. Vui lòng thử lại", null),
                DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => (409, "Dữ liệu đã tồn tại hoặc yêu cầu đã được xử lý", null),
                BadHttpRequestException => (400, "Yêu cầu không hợp lệ", null),
                _ => (500, "Không thể xử lý yêu cầu lúc này", null)
            };
            // Do not log exception SQL parameters or user supplied values.
            if (status >= 500) logger.LogError("Request {TraceId} failed: {ExceptionType}", context.TraceIdentifier, ex.GetType().Name);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>(false, message, null, errors));
        }
    }
}
