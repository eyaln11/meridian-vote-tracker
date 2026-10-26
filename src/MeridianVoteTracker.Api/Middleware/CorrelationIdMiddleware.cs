using System.Text.RegularExpressions;

namespace MeridianVoteTracker.Api.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    private static readonly Regex ValidId = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        
        var correlationId = incoming is not null && ValidId.IsMatch(incoming) 
            ? incoming 
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        
        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            logger.LogInformation("Handling {Method} {Path}", context.Request.Method, context.Request.Path);
            await next(context);
        }

    }

}