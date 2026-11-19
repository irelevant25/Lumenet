namespace DmxBox.Tests;

/// <summary>
/// The server answered a call of a <see cref="HubClient"/> with an error. The message is the
/// error text exactly as it was on the wire, which is what a browser gets too.
/// </summary>
internal sealed class HubCallException(string message) : Exception(message);
