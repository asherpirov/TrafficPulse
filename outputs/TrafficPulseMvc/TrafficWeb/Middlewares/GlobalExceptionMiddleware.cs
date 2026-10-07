using TrafficShared.Models;
namespace TrafficWeb.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger) { _next = next; _logger = logger; }
    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;
            int status = ex is BusinessException business ? business.StatusCode : 500;
            if (status == 500) _logger.LogError(ex, "Request failed. TraceId: {TraceId}", context.TraceIdentifier);
            context.Response.Clear();
            context.Response.StatusCode = status;
            string message = ex is BusinessException ? ex.Message : "אירעה תקלה. נסו שוב מאוחר יותר.";
            if (context.Request.Path.StartsWithSegments("/api"))
                await context.Response.WriteAsJsonAsync(new { status, title = message, traceId = context.TraceIdentifier });
            else
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync("<!doctype html><html lang='he' dir='rtl'><meta charset='utf-8'><title>TrafficPulse</title><body><h1>" +
                    System.Net.WebUtility.HtmlEncode(message) + "</h1><a href='/'>חזרה לאתר</a></body></html>");
            }
        }
    }
}
