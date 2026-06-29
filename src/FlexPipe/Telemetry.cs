using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace FlexPipe;

internal static class Telemetry
{
    private static readonly string Version =
        typeof(Telemetry).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";

    internal static readonly ActivitySource Source = new("FlexPipe", Version);
    internal static readonly Meter Meter = new("FlexPipe", Version);

    internal static readonly Histogram<double> PipelineDuration =
        Meter.CreateHistogram<double>("flexpipe.pipeline.duration", "ms",
            "Wall-clock duration of the full pipeline execution.");

    internal static readonly Histogram<double> TaskDuration =
        Meter.CreateHistogram<double>("flexpipe.task.duration", "ms",
            "Wall-clock duration of a single task execution.");
}
