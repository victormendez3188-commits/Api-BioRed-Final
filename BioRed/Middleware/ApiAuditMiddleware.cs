using System.Diagnostics;
using System.Security.Claims;
using BioRed.Application.Reporting;

namespace BioRed.Middleware;

public sealed class ApiAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiAuditMiddleware> _logger;

    public ApiAuditMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<ApiAuditMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var statusCode = StatusCodes.Status500InternalServerError;

        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            stopwatch.Stop();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var writer = scope.ServiceProvider.GetRequiredService<IApiAuditWriter>();
                var accountClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var accountId = int.TryParse(accountClaim, out var parsedAccountId)
                    ? parsedAccountId
                    : (int?)null;

                await writer.WriteAsync(
                    new ApiAuditWriteRequest(
                        accountId,
                        context.User.FindFirstValue("user_type"),
                        context.User.FindFirstValue("reference_id"),
                        ResolveModule(context.Request.Path),
                        context.Request.Method,
                        context.Request.Path.Value ?? "/",
                        statusCode,
                        stopwatch.ElapsedMilliseconds,
                        context.Connection.RemoteIpAddress?.ToString(),
                        context.Request.Headers["User-Agent"].ToString(),
                        context.TraceIdentifier,
                        startedAtUtc),
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "No fue posible registrar la solicitud {Method} {Path} en la bitácora.",
                    context.Request.Method,
                    context.Request.Path);
            }
        }
    }

    private static string ResolveModule(PathString path)
    {
        var segments = (path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 3
            ? segments[2].ToLowerInvariant()
            : "api";
    }
}
