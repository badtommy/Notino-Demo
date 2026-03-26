using System.Text.Json;

namespace NotinoDemo.Middleware;

public sealed class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteClefEventAsync(context, ex);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = new
            {
                error = "An unexpected error occurred.",
                exceptionType = ex.GetType().Name,
                message = ex.Message
            };

            await context.Response.WriteAsJsonAsync(payload);
        }
    }

    private async Task WriteClefEventAsync(HttpContext context, Exception ex)
    {
        var logDirectory = Path.Combine(_environment.ContentRootPath, "logs");
        Directory.CreateDirectory(logDirectory);

        var logPath = Path.Combine(logDirectory, "notino-errors.json");
        var logEvent = new Dictionary<string, object?>
        {
            ["@t"] = DateTime.UtcNow.ToString("O"),
            ["@mt"] = "Unhandled exception on {Method} {Path} | ExceptionType={ExceptionType} | Message={Message}",
            ["@l"] = "Error",
            ["@x"] = ex.ToString(),
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path.ToString(),
            ["ExceptionType"] = ex.GetType().Name,
            ["Message"] = ex.Message
        };

        var line = JsonSerializer.Serialize(logEvent) + Environment.NewLine;

        await FileLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(logPath, line);
        }
        finally
        {
            FileLock.Release();
        }
    }
}

public static class ExceptionLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionLoggingMiddleware>();
    }
}
