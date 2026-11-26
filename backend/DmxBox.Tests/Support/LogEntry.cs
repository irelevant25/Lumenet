using Microsoft.Extensions.Logging;

namespace DmxBox.Tests.Support;

/// <summary>One entry of the server's log, as a <see cref="RecordingLog"/> kept it.</summary>
/// <param name="Level">How serious: Information, Warning, Error ...</param>
/// <param name="Category">Who wrote it, for example "DmxBox.Server.Api.DeskHub".</param>
/// <param name="Message">The line of text.</param>
/// <param name="Exception">The exception that came with it. On the console that is the stack trace.</param>
internal sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);
