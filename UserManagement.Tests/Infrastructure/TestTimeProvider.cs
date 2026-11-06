namespace UserManagement.Tests.Infrastructure;

public sealed class TestTimeProvider : TimeProvider
{
    private long _utcTicks = TestDates.InitialUtc.Ticks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

    public void Advance(TimeSpan interval) => Interlocked.Add(ref _utcTicks, interval.Ticks);
}
