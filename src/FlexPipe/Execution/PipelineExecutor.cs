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
        async Task ExecuteTasks()
        {
            foreach (var task in _tasks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (context.IsFailed)
                    break;

                try
                {
                    await task.Execute(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    context.Fail(ex);
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

        return new PipelineResult<TOutput>(context.Output, context.Errors);
    }
}
