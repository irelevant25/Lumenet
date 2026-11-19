namespace DmxBox.Server;

/// <summary>The answer of GET /api/health: proof that this is the Lumenet server, and which one.</summary>
/// <param name="Name">Always "Lumenet".</param>
/// <param name="Version">The server's version, for example "0.1.0".</param>
internal sealed record HealthAnswer(string Name, string Version);
