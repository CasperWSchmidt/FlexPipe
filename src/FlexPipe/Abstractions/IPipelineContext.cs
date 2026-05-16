namespace FlexPipe;

public interface IPipelineContext<TInput, TOutput>
{
    TInput Input { get; }

    TOutput Output { get; set; }

    bool IsFailed { get; }

    IReadOnlyList<Exception> Errors { get; }

    void Fail(string error);

    void Fail(Exception exception);
}
