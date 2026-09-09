using Microsoft.AspNetCore.SignalR;
using Server.Core.Game;
using Server.Utils;

namespace Server.Middleware;

// SignalR IHubFilter, which is what actually sees every individual hub method call (from GameHub)
// (real middleware only sees the initial HTTP handshake, not the WebSocket traffic after that).
public sealed class RateLimitMiddleware : IHubFilter
{
    private readonly RateLimiter _limiter = new(GameConstants.RateLimitMaxCalls, GameConstants.RateLimitWindowMs);

    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!_limiter.TryConsume(invocationContext.Context.ConnectionId, DateTime.UtcNow))
            throw new HubException("Rate limit exceeded");

        return next(invocationContext);
    }

    public Task OnDisconnectedAsync(
        HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        _limiter.Remove(context.Context.ConnectionId);
        return next(context, exception);
    }
}
