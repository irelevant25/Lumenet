namespace Lumenet;

/// <summary>A line of the log.</summary>
/// <param name="Number">Counts the lines from the start: the log window shows those after the last it showed.</param>
/// <param name="Time">When it was written, in the time of this PC.</param>
/// <param name="Level">How serious it is.</param>
/// <param name="Category">Who wrote it: "BoxLink", "Microsoft.AspNetCore.Server.Kestrel".</param>
/// <param name="Text">What it says, with the error under it if one came with it.</param>
internal sealed record LogLine(long Number, DateTime Time, LogLevel Level, string Category, string Text);

/// <summary>
/// The log of the desk program, which a console would show: the newest lines, as many as
/// <see cref="Capacity"/>, kept for the log window, which can open at any time. It is the
/// desk program's logger as well.
/// </summary>
internal sealed class LogBook : ILoggerProvider
{
    /// <summary>The newest lines kept; older ones go.</summary>
    public const int Capacity = 2000;

    // The desk program's own names are shown without their namespace: "BoxLink".
    private const string OwnNamespace = "DmxBox.Server.";

    private readonly object _lock = new();
    private readonly Queue<LogLine> _lines = new();
    private long _count;

    /// <summary>A line came, on whatever thread wrote it.</summary>
    public event Action? Added;

    /// <summary>Adds a line, also one that is not the desk program's (Lumenet's own).</summary>
    public void Add(LogLevel level, string category, string message, Exception? error = null)
    {
        string text = error is null ? message : $"{message}{Environment.NewLine}{error}";
        if (category.StartsWith(OwnNamespace, StringComparison.Ordinal))
            category = category[OwnNamespace.Length..];

        lock (_lock)
        {
            _lines.Enqueue(new LogLine(++_count, DateTime.Now, level, category, text));
            while (_lines.Count > Capacity)
                _lines.Dequeue();
        }

        Added?.Invoke();
    }

    /// <summary>The lines kept that came after the one with this number, the oldest first.</summary>
    public IReadOnlyList<LogLine> LinesAfter(long number)
    {
        lock (_lock)
            return _lines.Where(line => line.Number > number).ToArray();
    }

    ILogger ILoggerProvider.CreateLogger(string categoryName)
    {
        return new Writer(this, categoryName);
    }

    // The host disposes its loggers when it stops; the lines stay for the log window.
    void IDisposable.Dispose()
    {
    }

    private sealed class Writer(LogBook book, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                book.Add(logLevel, category, formatter(state, exception), exception);
        }
    }
}
