using System.Diagnostics;
using System.Diagnostics.Metrics;
using FlexPipe.Tests.Helpers;
using Xunit;

namespace FlexPipe.Tests.Unit;

// Prevents parallel execution with other test classes so that ActivityListeners
// registered in these tests don't accidentally capture activities emitted by
// PipelineExecutor calls in concurrently-running test classes.
[CollectionDefinition("TelemetryTests", DisableParallelization = true)]
public class TelemetryTestsCollection { }

[Collection("TelemetryTests")]
public class TelemetryTests
{
    static PipelineExecutor<TestInput, TestOutput> CreateExecutor(
        params IPipelineTask<TestInput, TestOutput>[] tasks)
        => new(tasks, []);

    static TestContext<TestInput, TestOutput> CreateContext()
        => new(new TestInput());

    static ActivityListener CreateListener(List<Activity> captured) => new()
    {
        ShouldListenTo = s => s.Name == "FlexPipe",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        ActivityStopped = a => captured.Add(a),
    };

    [Fact]
    public async Task Execute_EmitsPipelineExecuteSpan_WithInputOutputAndSucceededAttributes()
    {
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        ActivitySource.AddActivityListener(listener);

        await CreateExecutor(new FuncTask()).Execute(CreateContext());

        var span = Assert.Single(activities, a => a.OperationName == "pipeline.execute");
        Assert.Equal(typeof(TestInput).Name, span.GetTagItem("pipeline.input_type"));
        Assert.Equal(typeof(TestOutput).Name, span.GetTagItem("pipeline.output_type"));
        Assert.Equal("true", span.GetTagItem("pipeline.succeeded"));
    }

    [Fact]
    public async Task Execute_EmitsPipelineTaskSpan_WithTaskTypeAndSucceededAttributes()
    {
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        ActivitySource.AddActivityListener(listener);

        await CreateExecutor(new FuncTask()).Execute(CreateContext());

        var span = Assert.Single(activities, a => a.OperationName == "pipeline.task");
        Assert.Equal(nameof(FuncTask), span.GetTagItem("pipeline.task_type"));
        Assert.Equal("true", span.GetTagItem("pipeline.succeeded"));
    }

    [Fact]
    public async Task Execute_TaskSpanIsNestedUnderPipelineSpan()
    {
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        ActivitySource.AddActivityListener(listener);

        await CreateExecutor(new FuncTask()).Execute(CreateContext());

        var pipelineSpan = activities.Single(a => a.OperationName == "pipeline.execute");
        var taskSpan = activities.Single(a => a.OperationName == "pipeline.task");
        Assert.Equal(pipelineSpan.SpanId, taskSpan.ParentSpanId);
    }

    [Fact]
    public async Task Execute_EmitsOneTaskSpanPerTask()
    {
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        ActivitySource.AddActivityListener(listener);

        await CreateExecutor(new FuncTask(), new FuncTask(), new FuncTask())
            .Execute(CreateContext());

        Assert.Equal(3, activities.Count(a => a.OperationName == "pipeline.task"));
    }

    [Fact]
    public async Task Execute_PipelineSpan_HasErrorStatus_WhenFailed()
    {
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        ActivitySource.AddActivityListener(listener);

        await CreateExecutor(
            new FuncTask((ctx, _) => { ctx.Fail("something broke"); return Task.CompletedTask; }))
            .Execute(CreateContext());

        var span = activities.Single(a => a.OperationName == "pipeline.execute");
        Assert.Equal("false", span.GetTagItem("pipeline.succeeded"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Contains("something broke", span.StatusDescription);
    }

    [Fact]
    public async Task Execute_TaskSpan_HasErrorStatus_WhenTaskCallsFail()
    {
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        ActivitySource.AddActivityListener(listener);

        await CreateExecutor(
            new FuncTask((ctx, _) => { ctx.Fail("task error"); return Task.CompletedTask; }))
            .Execute(CreateContext());

        var span = activities.Single(a => a.OperationName == "pipeline.task");
        Assert.Equal("false", span.GetTagItem("pipeline.succeeded"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public async Task Execute_TaskSpan_RecordsExceptionEvent_WhenTaskThrows()
    {
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        ActivitySource.AddActivityListener(listener);

        await CreateExecutor(
            new FuncTask((_, _) => throw new InvalidOperationException("boom")))
            .Execute(CreateContext());

        var span = activities.Single(a => a.OperationName == "pipeline.task");
        Assert.Equal("false", span.GetTagItem("pipeline.succeeded"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("boom", span.StatusDescription);
        var exEvent = Assert.Single(span.Events, e => e.Name == "exception");
        Assert.Equal("System.InvalidOperationException",
            exEvent.Tags.First(t => t.Key == "exception.type").Value);
        Assert.Equal("boom",
            exEvent.Tags.First(t => t.Key == "exception.message").Value);
    }

    [Fact]
    public async Task Execute_RecordsPipelineDurationHistogram()
    {
        var measurements = new List<(double Value, string Succeeded)>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "FlexPipe" && instrument.Name == "flexpipe.pipeline.duration")
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            var succeeded = tags.ToArray().First(t => t.Key == "pipeline.succeeded").Value as string ?? "";
            measurements.Add((value, succeeded));
        });
        meterListener.Start();

        await CreateExecutor(new FuncTask()).Execute(CreateContext());

        var (v, succeeded) = Assert.Single(measurements);
        Assert.True(v >= 0);
        Assert.Equal("true", succeeded);
    }

    [Fact]
    public async Task Execute_RecordsTaskDurationHistogram_OncePerTask()
    {
        var measurements = new List<double>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "FlexPipe" && instrument.Name == "flexpipe.task.duration")
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>((_, value, _, _) => measurements.Add(value));
        meterListener.Start();

        await CreateExecutor(new FuncTask(), new FuncTask()).Execute(CreateContext());

        Assert.Equal(2, measurements.Count);
        Assert.All(measurements, v => Assert.True(v >= 0));
    }
}
