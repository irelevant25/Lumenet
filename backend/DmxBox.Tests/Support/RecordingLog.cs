using Microsoft.Extensions.Logging;

namespace DmxBox.Tests.Support;

/// <summary>
/// A log for the tests to read. Added to the server next to its console log, it gets exactly
/// the entries the console gets: the same rules decide what is written and what is left out.
/// </summary>
/// <remarks>Thread-safe: the server writes from many threads while the test reads.</remarks>
internal sealed class RecordingLog : ILoggerProvider
{
    private readonly Lock _lock = new();
    private readonly List<LogEntry> _entries = [];

    /// <summary>Everything written so far, in order.</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <summary>Makes the logger for one category. The logging system calls this.</summary>
    public ILogger CreateLogger(string categoryName)
    {
        return new CategoryLog(this, categoryName);
    }

    /// <summary>Nothing to release.</summary>
    public void Dispose()
    {
    }

    private void Add(LogEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }
    }

    // What the logging system writes to: one of these per category.
    private sealed class CategoryLog(RecordingLog log, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            log.Add(new LogEntry(logLevel, category, formatter(state, exception), exception));
        }
    }
}
