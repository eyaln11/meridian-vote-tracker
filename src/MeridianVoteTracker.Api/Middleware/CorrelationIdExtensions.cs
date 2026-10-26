namespace MeridianVoteTracker.Api.Middleware;

public static class CorrelationIdExtensions
{
    public static string? GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) ? value as string : null;
}