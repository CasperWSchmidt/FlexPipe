namespace FlexPipe;

public class PipelineResult<TOutput>
{
    public TOutput? Output { get; }
    public IReadOnlyList<Exception> Errors { get; }
    public bool HasSucceeded => Errors.Count == 0;

    internal PipelineResult(TOutput? output, IReadOnlyList<Exception> errors)
    {
        Output = output;
        Errors = errors;
    }

    public TOutput EnsureSuccess()
    {
        if (!HasSucceeded)
            throw new PipelineException(Errors);

        return Output!;
    }
}

public class PipelineException : Exception
{
    public IReadOnlyList<Exception> Errors { get; }

    public PipelineException(IReadOnlyList<Exception> errors)
        : base(string.Join("; ", errors.Select(e => e.Message)))
    {
        Errors = errors;
    }
}
