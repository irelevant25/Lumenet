using System.Diagnostics;

namespace DmxBox.Tests;

/// <summary>Waiting for something that another thread does, without sleeping a fixed time.</summary>
internal static class Wait
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>Waits until the condition holds. Fails the test if it does not within 5 seconds.</summary>
    /// <param name="condition">Looked at every 10 ms.</param>
    /// <param name="failure">What to say when the time is up.</param>
    public static async Task UntilAsync(Func<bool> condition, string failure)
    {
        var waited = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(waited.Elapsed < Patience, failure);
            await Task.Delay(10);
        }
    }
}
