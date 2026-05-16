namespace FlexPipe;

public interface IPipelineExecutor<TInput, TOutput>
{
    Task<PipelineResult<TOutput>> Execute(
        IPipelineContext<TInput, TOutput> context,
        CancellationToken cancellationToken = default);
}
