namespace FlexPipe;

public interface IPipelineMiddleware<TInput, TOutput>
{
    Task Execute(
        IPipelineContext<TInput, TOutput> context,
        Func<Task> next,
        CancellationToken cancellationToken);
}
