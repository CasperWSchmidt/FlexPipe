using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FlexPipe.Tests.Helpers;

/// <summary>
/// A single captured log entry: its level, rendered message, exception, the structured
/// state key/value pairs, the active scopes at log time, and the ambient trace id.
/// </summary>
internal sealed record LogEntry(
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    IReadOnlyList<object?> Scopes,
    string? TraceId)
{
    /// <summary>Finds a value for <paramref name="key"/> across all active scopes.</summary>
    public bool TryGetScopeValue(string key, out object? value)
    {
        foreach (var scope in Scopes)
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var pair in pairs)
                {
                    if (pair.Key == key)
                    {
                        value = pair.Value;
                        return true;
                    }
                }
            }
        }

        value = null;
        return false;
    }
}

internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    public List<LogEntry> Entries { get; } = [];
    public List<string> Categories { get; } = [];

    public ILogger CreateLogger(string categoryName)
    {
        Categories.Add(categoryName);
        return new CapturingLogger(Entries);
    }

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
}

internal sealed class CapturingLogger(List<LogEntry> entries) : ILogger
{
    // Execution within a single pipeline run is logically single-threaded (awaits resume
    // sequentially, no fan-out), so a plain stack faithfully reflects nested scopes.
    private readonly Stack<object?> _scopes = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        _scopes.Push(state);
        return new PopScope(_scopes);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var statePairs = state as IReadOnlyList<KeyValuePair<string, object?>>
            ?? [];

        entries.Add(new LogEntry(
            logLevel,
            formatter(state, exception),
            exception,
            statePairs,
            _scopes.ToArray(),
            Activity.Current?.TraceId.ToString()));
    }

    private sealed class PopScope(Stack<object?> scopes) : IDisposable
    {
        public void Dispose()
        {
            if (scopes.Count > 0)
                scopes.Pop();
        }
    }
}
