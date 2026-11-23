namespace DmxBox.Server;

/// <summary>The answer to a UI's first call, "Hello": everything it needs to draw itself.</summary>
/// <param name="Version">The server's version, for example "0.1.0".</param>
/// <param name="Desk">The desk as it is now.</param>
/// <param name="Boxes">The boxes, which of them are connected and what they play, see <see cref="BoxNetwork.Boxes"/>.</param>
/// <param name="Project">The project that is open.</param>
internal sealed record HelloAnswer(string Version, DeskState Desk, BoxView[] Boxes, ProjectInfo Project);
