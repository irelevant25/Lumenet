using System.Text.Json;

namespace DmxBox.Tests.Support;

/// <summary>A message the server sent to a <see cref="HubClient"/> on its own.</summary>
/// <param name="Name">The message's name, for example "ChannelChanged".</param>
/// <param name="Arguments">What came with it, as the JSON that was on the wire.</param>
internal sealed record HubMessage(string Name, JsonElement[] Arguments);
