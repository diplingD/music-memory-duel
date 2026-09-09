using Server.Utils;

namespace Server.Tests;

public class RateLimiterTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AllowsCallsUpToLimit()
    {
        var limiter = new RateLimiter(maxCalls: 3, windowMs: 10_000);

        Assert.True(limiter.TryConsume("conn-1", Now));
        Assert.True(limiter.TryConsume("conn-1", Now));
        Assert.True(limiter.TryConsume("conn-1", Now));
    }

    [Fact]
    public void RejectsCallsOverLimitWithinWindow()
    {
        var limiter = new RateLimiter(maxCalls: 3, windowMs: 10_000);

        limiter.TryConsume("conn-1", Now);
        limiter.TryConsume("conn-1", Now);
        limiter.TryConsume("conn-1", Now);

        Assert.False(limiter.TryConsume("conn-1", Now.AddMilliseconds(500)));
    }

    [Fact]
    public void ResetsAfterWindowElapses()
    {
        var limiter = new RateLimiter(maxCalls: 3, windowMs: 10_000);

        limiter.TryConsume("conn-1", Now);
        limiter.TryConsume("conn-1", Now);
        limiter.TryConsume("conn-1", Now);
        Assert.False(limiter.TryConsume("conn-1", Now.AddMilliseconds(500)));

        Assert.True(limiter.TryConsume("conn-1", Now.AddMilliseconds(10_001)));
    }

    [Fact]
    public void TracksEachKeyIndependently()
    {
        var limiter = new RateLimiter(maxCalls: 1, windowMs: 10_000);

        Assert.True(limiter.TryConsume("conn-1", Now));
        Assert.True(limiter.TryConsume("conn-2", Now));
        Assert.False(limiter.TryConsume("conn-1", Now));
    }

    [Fact]
    public void RemoveForgetsKeyState()
    {
        var limiter = new RateLimiter(maxCalls: 1, windowMs: 10_000);

        limiter.TryConsume("conn-1", Now);
        limiter.Remove("conn-1");

        Assert.True(limiter.TryConsume("conn-1", Now));
    }
}
