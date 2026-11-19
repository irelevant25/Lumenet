namespace DmxBox.Cli;

/// <summary>The command line could not be understood. The message says why.</summary>
internal sealed class UsageException(string message) : Exception(message);
