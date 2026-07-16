namespace FlexPipe;

/// <summary>
/// Options controlling FlexPipe's native structured logging. Logging is quiet by
/// default (success at <c>Debug</c>, per-task at <c>Trace</c>, failures at
/// <c>Warning</c>/<c>Error</c>); these knobs only adjust the surface, not the levels.
/// </summary>
public class FlexPipeOptions
{
    /// <summary>
    /// When <see langword="true"/> (the default), each task emits per-task log entries
    /// (started/completed at <c>Trace</c>, failures at <c>Warning</c>) under a nested
    /// <c>task</c> scope. Set to <see langword="false"/> to suppress per-task logging
    /// entirely, leaving only the pipeline-level entries.
    /// </summary>
    public bool EnableTaskLogging { get; set; } = true;

    /// <summary>
    /// The <see cref="Microsoft.Extensions.Logging.ILogger"/> category name used for
    /// FlexPipe's log entries. Defaults to <c>"FlexPipe"</c>, matching the
    /// <c>ActivitySource</c> and <c>Meter</c> names so all three pillars share a name.
    /// </summary>
    public string LogCategory { get; set; } = "FlexPipe";
}
