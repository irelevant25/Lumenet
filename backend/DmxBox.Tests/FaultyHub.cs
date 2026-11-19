using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Tests;

/// <summary>
/// A hub with a fault in it, for the tests of the log. The real hub has no method that fails
/// in a way the user did not cause, and the tests need one to see how the server reports it.
/// </summary>
internal sealed class FaultyHub : Hub
{
    /// <summary>What the fault says. It must reach the log, and must not reach the UI.</summary>
    public const string Fault = "something is wrong inside the program";

    /// <summary>Always fails, the way a mistake in the program would.</summary>
    public void Break()
    {
        throw new InvalidOperationException(Fault);
    }
}
