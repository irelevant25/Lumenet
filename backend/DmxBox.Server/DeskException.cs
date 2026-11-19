namespace DmxBox.Server;

/// <summary>
/// The desk cannot do what the user asked, for example load an empty memory or read a damaged
/// project file. The message is written for the user: the hub passes it on to the UI as it is.
/// </summary>
internal sealed class DeskException(string message) : Exception(message);
