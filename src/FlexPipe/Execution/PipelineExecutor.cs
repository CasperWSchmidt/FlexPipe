using System.Diagnostics;

namespace FlexPipe;

public class PipelineExecutor<TInput, TOutput> : IPipelineExecutor<TInput, TOutput>
{
    private readonly IPipelineTask<TInput, TOutput>[] _tasks;
    private readonly IPipelineMiddleware<TInput, TOutput>[] _middlewares;

    public PipelineExecutor(
        IEnumerable<IPipelineTask<TInput, TOutput>> tasks,
        IEnumerable<IPipelineMiddleware<TInput, TOutput>> middlewares)
    {
        _tasks = tasks.ToArray();
        _middlewares = middlewares.ToArray();
    }

    public async Task<PipelineResult<TOutput>> Execute(
        IPipelineContext<TInput, TOutput> context,
        CancellationToken cancellationToken = default)
    {
        using var pipelineActivity = Telemetry.Source.StartActivity("pipeline.execute");
        pipelineActivity?.SetTag("pipeline.input_type", typeof(TInput).Name);
        pipelineActivity?.SetTag("pipeline.output_type", typeof(TOutput).Name);

        var pipelineSw = Stopwatch.StartNew();

        async Task ExecuteTasks()
        {
            foreach (var task in _tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (context.IsFailed)
                    break;

                using var taskActivity = Telemetry.Source.StartActivity("pipeline.task");
                taskActivity?.SetTag("pipeline.task_type", task.GetType().Name);

                var taskSw = Stopwatch.StartNew();
                var taskSucceeded = true;
                try
                {
                    await task.Execute(context, cancellationToken);
                    taskSucceeded = !context.IsFailed;
                    taskActivity?.SetTag("pipeline.succeeded", taskSucceeded ? "true" : "false");
                    if (!taskSucceeded)
                        taskActivity?.SetStatus(ActivityStatusCode.Error, "Task called context.Fail()");
                }
                catch (Exception ex)
                {
                    taskSucceeded = false;
                    context.Fail(ex);
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

        await NextMiddleware();

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

        return result;
    }
}
