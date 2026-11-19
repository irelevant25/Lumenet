namespace DmxBox.Tests;

/// <summary>
/// Not part of the Python test suite: what the client does when nobody answers.
/// On Windows a closed UDP port shows up as a socket error on the next receive call;
/// the client has to treat that like silence.
/// </summary>
public sealed class NoAnswerTests
{
    [Fact]
    public void SwitchedOffBoxEndsInTimeoutAfterAllAttempts()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient(TimeSpan.FromMilliseconds(20), attempts: 3);
        box.Ping();
        int retransmitsWhileOn = box.Retransmits;

        sim.Stop();

        Assert.Throws<DmxBoxTimeoutException>(() => box.Ping());
        Assert.Equal(retransmitsWhileOn + 2, box.Retransmits);   // the second and the third attempt
    }
}
