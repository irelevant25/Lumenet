namespace DmxBox.Sim;

/// <summary>
/// A simulated DMX device. Plugged in, it is there for the desk: it says so in the registry of the
/// simulators (<see cref="SimulatorRegistry"/>), answers as the real device does and shows what it
/// would put out on its DMX cables. Pulled out, it is gone, as a device whose cable is pulled.
/// </summary>
internal interface ISimulator : IDisposable
{
    /// <summary>What it is: "dmxbox with the Pico, one output".</summary>
    string Title { get; }

    /// <summary>How the desk finds it, a line or two.</summary>
    string Finding { get; }

    /// <summary>The keys of its own beyond U and Q, with what they do: B for the blackout button.</summary>
    IReadOnlyList<(char Key, string What)> Keys { get; }

    /// <summary>True while it is plugged in.</summary>
    bool PluggedIn { get; }

    /// <summary>Plugs it in: it answers, and the desk finds it.</summary>
    void PlugIn();

    /// <summary>Pulls it out: it is gone for the desk, until it is plugged in again.</summary>
    void PullOut();

    /// <summary>A key of its own was pressed.</summary>
    /// <param name="key">The key, in capitals.</param>
    void Press(char key);

    /// <summary>Called about 20 times a second: writes what changed.</summary>
    void Tick();
}
