using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceLogistics.Api.Web;

public sealed class ApiEnvelopeMiddleware(RequestDelegate next, ILogger<ApiEnvelopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.TraceIdentifier = Guid.NewGuid().ToString("N");
        context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-store";
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, code, message) = ex switch
            {
                IdentityFault f => (f.Status, f.Code, f.Message),
                SqlException or DbUpdateException => (503, "DATABASE_UNAVAILABLE", "Không thể truy cập dữ liệu hệ thống. Vui lòng thử lại sau."),
                _ => (500, "SERVER_ERROR", "Có lỗi xử lý. Vui lòng cung cấp mã yêu cầu cho IT.")
            };
            if (status >= 500)
                logger.LogError("Request {CorrelationId} failed ({ErrorType}, {ErrorCode}).", context.TraceIdentifier, ex.GetType().Name, code);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ApiError(code, message, context.TraceIdentifier), context.RequestAborted);
        }
    }
}
