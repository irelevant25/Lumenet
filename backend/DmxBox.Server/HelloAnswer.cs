namespace DmxBox.Server;

/// <summary>The answer to a UI's first call, "Hello": everything it needs to draw itself.</summary>
/// <param name="Version">The server's version, for example "0.1.0".</param>
/// <param name="Desk">The desk as it is now.</param>
/// <param name="Boxes">The boxes, which of them are connected and what they play, see <see cref="BoxNetwork.Boxes"/>.</param>
/// <param name="Project">The project that is open.</param>
/// <param name="Outputs">The outputs besides the boxes, with their states, see <see cref="OutputNetwork.Outputs"/>.</param>
internal sealed record HelloAnswer(string Version, DeskState Desk, BoxView[] Boxes, ProjectInfo Project, OutputView[] Outputs)
{
    /// <summary>The Soundcraft Ui mixer of the tracks: whether it answers, what it is, its playlist.</summary>
    public MixerView Mixer { get; init; } = new(UiMixer.None, "");

    /// <summary>LISTEN ON: whether what goes out plays on the device to listen on as well.</summary>
    public ListenView Listen { get; init; } = new(false, "");
}
