using System.Diagnostics;
using FlexPipe.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FlexPipe.Tests.Unit;

// Shares the telemetry collection so ActivityListeners registered here don't race with
// activities emitted by executors in concurrently-running test classes.
[Collection("TelemetryTests")]
public class LoggingTests
{
    static PipelineExecutor<TestInput, TestOutput> CreateExecutor(
        CapturingLoggerFactory factory,
        FlexPipeOptions? options = null,
        params IPipelineTask<TestInput, TestOutput>[] tasks)
        => new(tasks, [], factory, options);

    static TestContext<TestInput, TestOutput> CreateContext()
        => new(new TestInput());

    [Fact]
    public async Task Execute_LogsPipelineStartAndCompletion_AtDebug_OnSuccess()
    {
        var factory = new CapturingLoggerFactory();

        await CreateExecutor(factory, tasks: new FuncTask()).Execute(CreateContext());

        var pipelineEntries = factory.Entries
            .Where(e => e.Message.StartsWith("Pipeline "))
            .ToList();
        Assert.Equal(2, pipelineEntries.Count);
        Assert.All(pipelineEntries, e => Assert.Equal(LogLevel.Debug, e.Level));

        var started = pipelineEntries[0];
        Assert.Contains("started", started.Message);
        Assert.Equal(nameof(TestInput), started.State.Single(s => s.Key == "Pipeline").Value);

        var completed = pipelineEntries[1];
        Assert.Contains("completed", completed.Message);
        Assert.Equal(nameof(TestInput), completed.State.Single(s => s.Key == "Pipeline").Value);
        // Metadata only: duration present, no Input/Output payload fields.
        Assert.Contains(completed.State, s => s.Key == "ElapsedMs");
        Assert.All(completed.State, s => Assert.DoesNotContain("Input", s.Key == "{OriginalFormat}" ? "" : s.Key));
        Assert.DoesNotContain(completed.State, s => s.Key is "Input" or "Output");
    }

    [Fact]
    public async Task Execute_LogsWarning_WithErrorMessages_OnFailure()
    {
        var factory = new CapturingLoggerFactory();

        await CreateExecutor(factory, tasks:
                new FuncTask((ctx, _) => { ctx.Fail("something broke"); return Task.CompletedTask; }))
            .Execute(CreateContext());

        var warning = Assert.Single(factory.Entries,
            e => e.Level == LogLevel.Warning && e.Message.StartsWith("Pipeline "));
        Assert.Contains("failed", warning.Message);
        Assert.Contains("something broke", warning.Message);
        Assert.Equal(nameof(TestInput), warning.State.Single(s => s.Key == "Pipeline").Value);
        Assert.Contains(warning.State, s => s.Key == "ElapsedMs");
        // A failed pipeline logs a Warning instead of the Debug "completed" line.
        Assert.DoesNotContain(factory.Entries,
            e => e.Message.StartsWith("Pipeline ") && e.Message.Contains("completed"));
    }

    [Fact]
    public async Task Execute_LogsError_AndRethrows_OnUnhandledException()
    {
        var factory = new CapturingLoggerFactory();
        var boom = new InvalidOperationException("middleware blew up");
        // Task exceptions are caught and turned into failures; a throwing middleware
        // surfaces as a genuinely unhandled exception at the pipeline level.
        PipelineExecutor<TestInput, TestOutput> executor = new(
            [], [new FuncMiddleware((_, _, _) => throw boom)], factory);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.Execute(CreateContext()));

        Assert.Same(boom, thrown);
        var error = Assert.Single(factory.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("threw", error.Message);
        Assert.Equal(nameof(TestInput), error.State.Single(s => s.Key == "Pipeline").Value);
        Assert.Same(boom, error.Exception);
    }

    [Fact]
    public async Task Execute_LogsTaskTrace_StartAndCompletion_ForEachTask()
    {
        var factory = new CapturingLoggerFactory();

        await CreateExecutor(factory, tasks: new[] { new FuncTask(), new FuncTask() })
            .Execute(CreateContext());

        var taskEntries = factory.Entries.Where(e => e.Message.StartsWith("Task ")).ToList();
        Assert.All(taskEntries, e => Assert.Equal(LogLevel.Trace, e.Level));
        // Two tasks × (started + completed) = 4 Trace entries, all naming FuncTask.
        Assert.Equal(2, taskEntries.Count(e => e.Message.Contains("started")));
        Assert.Equal(2, taskEntries.Count(e => e.Message.Contains("completed")));
        Assert.All(taskEntries, e =>
            Assert.Equal(nameof(FuncTask), e.State.Single(s => s.Key == "Task").Value));
    }

    [Fact]
    public async Task Execute_SuppressesTaskLogs_WhenEnableTaskLoggingFalse()
    {
        var factory = new CapturingLoggerFactory();
        var options = new FlexPipeOptions { EnableTaskLogging = false };

        await CreateExecutor(factory, options, new FuncTask(), new FuncTask())
            .Execute(CreateContext());

        Assert.DoesNotContain(factory.Entries, e => e.Message.StartsWith("Task "));
        // Pipeline-level logging is unaffected.
        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Pipeline "));
    }

    [Fact]
    public async Task Execute_ScopeCarriesPipelineName_OnPipelineEntries()
    {
        var factory = new CapturingLoggerFactory();

        await CreateExecutor(factory, tasks: new FuncTask()).Execute(CreateContext());

        var pipelineEntry = factory.Entries.First(e => e.Message.StartsWith("Pipeline "));
        Assert.True(pipelineEntry.TryGetScopeValue("pipeline", out var pipeline));
        Assert.Equal(nameof(TestInput), pipeline);
    }

    [Fact]
    public async Task Execute_TaskEntries_CarryBothPipelineAndTaskScopes()
    {
        var factory = new CapturingLoggerFactory();

        await CreateExecutor(factory, tasks: new FuncTask()).Execute(CreateContext());

        var taskEntry = factory.Entries.First(e => e.Message.StartsWith("Task "));
        Assert.True(taskEntry.TryGetScopeValue("pipeline", out var pipeline));
        Assert.Equal(nameof(TestInput), pipeline);
        Assert.True(taskEntry.TryGetScopeValue("task", out var task));
        Assert.Equal(nameof(FuncTask), task);
    }

    [Fact]
    public async Task Execute_WithNoLoggerFactory_Succeeds_UsingNullLogger()
    {
        // No ILoggerFactory supplied — the executor falls back to NullLogger and runs
        // normally without any logging configured.
        PipelineExecutor<TestInput, TestOutput> executor = new(
            [new FuncTask((ctx, _) => { ctx.Output = new TestOutput { Value = "ok" }; return Task.CompletedTask; })],
            []);

        var result = await executor.Execute(CreateContext());

        Assert.True(result.HasSucceeded);
        Assert.Equal("ok", result.Output!.Value);
    }

    [Fact]
    public async Task Execute_LogEntries_CarryAmbientTraceId_WhenActivitySampled()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "FlexPipe",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        var factory = new CapturingLoggerFactory();
        await CreateExecutor(factory, tasks: new FuncTask()).Execute(CreateContext());

        var pipelineTraceId = activities.Single(a => a.OperationName == "pipeline.execute").TraceId.ToString();
        var started = factory.Entries.First(e => e.Message.StartsWith("Pipeline "));
        Assert.Equal(pipelineTraceId, started.TraceId);
        Assert.NotEqual("00000000000000000000000000000000", started.TraceId);
    }
}
