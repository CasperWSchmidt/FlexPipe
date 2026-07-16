using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlexPipe;

public class PipelineExecutor<TInput, TOutput> : IPipelineExecutor<TInput, TOutput>
{
    private readonly IPipelineTask<TInput, TOutput>[] _tasks;
    private readonly IPipelineMiddleware<TInput, TOutput>[] _middlewares;
    private readonly FlexPipeOptions _options;
    private readonly ILogger _logger;

    public PipelineExecutor(
        IEnumerable<IPipelineTask<TInput, TOutput>> tasks,
        IEnumerable<IPipelineMiddleware<TInput, TOutput>> middlewares,
        ILoggerFactory? loggerFactory = null,
        FlexPipeOptions? options = null)
    {
        _tasks = tasks.ToArray();
        _middlewares = middlewares.ToArray();
        _options = options ?? new FlexPipeOptions();
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(_options.LogCategory);
    }

    public async Task<PipelineResult<TOutput>> Execute(
        IPipelineContext<TInput, TOutput> context,
        CancellationToken cancellationToken = default)
    {
        var pipelineName = typeof(TInput).Name;

        using var pipelineActivity = Telemetry.Source.StartActivity("pipeline.execute");
        pipelineActivity?.SetTag("pipeline.input_type", pipelineName);
        pipelineActivity?.SetTag("pipeline.output_type", typeof(TOutput).Name);

        using var pipelineScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["pipeline"] = pipelineName,
        });

        var pipelineSw = Stopwatch.StartNew();
        _logger.LogDebug("Pipeline {Pipeline} started", pipelineName);

        async Task ExecuteTasks()
        {
            foreach (var task in _tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (context.IsFailed)
                    break;

                var taskName = task.GetType().Name;

                using var taskActivity = Telemetry.Source.StartActivity("pipeline.task");
                taskActivity?.SetTag("pipeline.task_type", taskName);

                using var taskScope = _options.EnableTaskLogging
                    ? _logger.BeginScope(new Dictionary<string, object> { ["task"] = taskName })
                    : null;

                var taskSw = Stopwatch.StartNew();
                var taskSucceeded = true;
                if (_options.EnableTaskLogging)
                    _logger.LogTrace("Task {Task} started", taskName);
                try
                {
                    await task.Execute(context, cancellationToken);
                    taskSucceeded = !context.IsFailed;
                    taskActivity?.SetTag("pipeline.succeeded", taskSucceeded ? "true" : "false");
                    if (!taskSucceeded)
                        taskActivity?.SetStatus(ActivityStatusCode.Error, "Task called context.Fail()");
                    if (_options.EnableTaskLogging)
                        _logger.LogTrace("Task {Task} completed in {ElapsedMs}ms",
                            taskName, taskSw.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    taskSucceeded = false;
                    context.Fail(ex);
                    if (_options.EnableTaskLogging)
                        _logger.LogWarning(ex, "Task {Task} failed after {ElapsedMs}ms",
                            taskName, taskSw.Elapsed.TotalMilliseconds);
                    taskActivity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                        { "exception.stacktrace", ex.ToString() },
                    }));
                    taskActivity?.SetTag("pipeline.succeeded", "false");
                    taskActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                }
                finally
                {
                    taskSw.Stop();
                    Telemetry.TaskDuration.Record(
                        taskSw.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("pipeline.task_type", task.GetType().Name),
                        new KeyValuePair<string, object?>("pipeline.succeeded", taskSucceeded ? "true" : "false"));
                }
            }
        }

        var index = -1;

        async Task NextMiddleware()
        {
            index++;

            if (index >= _middlewares.Length)
            {
                await ExecuteTasks();
                return;
            }

            await _middlewares[index].Execute(context, NextMiddleware, cancellationToken);
        }

        try
        {
            await NextMiddleware();
        }
        catch (Exception ex)
        {
            pipelineSw.Stop();
            _logger.LogError(ex, "Pipeline {Pipeline} threw after {ElapsedMs}ms",
                pipelineName, pipelineSw.Elapsed.TotalMilliseconds);
            throw;
        }

        var result = new PipelineResult<TOutput>(context.Output, context.Errors);

        pipelineSw.Stop();
        var pipelineSucceeded = result.HasSucceeded ? "true" : "false";
        pipelineActivity?.SetTag("pipeline.succeeded", pipelineSucceeded);
        if (!result.HasSucceeded)
            pipelineActivity?.SetStatus(ActivityStatusCode.Error,
                string.Join("; ", result.Errors.Select(e => e.Message)));

        Telemetry.PipelineDuration.Record(
            pipelineSw.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("pipeline.input_type", typeof(TInput).Name),
            new KeyValuePair<string, object?>("pipeline.output_type", typeof(TOutput).Name),
            new KeyValuePair<string, object?>("pipeline.succeeded", pipelineSucceeded));

        var elapsedMs = pipelineSw.Elapsed.TotalMilliseconds;
        if (result.HasSucceeded)
            _logger.LogDebug("Pipeline {Pipeline} completed in {ElapsedMs}ms", pipelineName, elapsedMs);
        else
            _logger.LogWarning("Pipeline {Pipeline} failed in {ElapsedMs}ms: {Errors}",
                pipelineName, elapsedMs, string.Join("; ", result.Errors.Select(e => e.Message)));

        return result;
    }
}
