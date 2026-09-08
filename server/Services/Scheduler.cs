namespace Server.Services;

public sealed class Scheduler
{
    public void Schedule(DateTime wakeAtUtc, Func<Task> action)
    {
        var delay = wakeAtUtc - DateTime.UtcNow;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            await action();
        });
    }
}
