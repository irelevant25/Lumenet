namespace DmxBox.Tests.Support;

/// <summary>
/// A clock the tests set by hand, for the parts of the desk that go by time: sequences and
/// the stack. No test has to wait for real time to pass.
/// </summary>
internal sealed class ManualTime : TimeProvider
{
    private long _ticks;

    /// <summary>One tick is a ten-millionth of a second, as with <see cref="TimeSpan"/>.</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override long GetTimestamp() => _ticks;

    /// <summary>Lets time pass.</summary>
    /// <param name="seconds">How many seconds.</param>
    public void Pass(double seconds)
    {
        _ticks += (long)Math.Round(seconds * TimeSpan.TicksPerSecond);
    }
}
