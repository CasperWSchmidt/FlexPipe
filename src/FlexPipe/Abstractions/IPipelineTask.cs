namespace FlexPipe;

public interface IPipelineTask<TInput, TOutput>
{
    Task Execute(
        IPipelineContext<TInput, TOutput> context,
        CancellationToken cancellationToken);
}
