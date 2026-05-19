namespace FlexPipe;

public class PipelineContext<TInput, TOutput> : IPipelineContext<TInput, TOutput>
{
    private readonly List<Exception> _errors = new();

    public required TInput Input { get; init; }
    public TOutput Output { get; set; } = default!;
    public bool IsFailed => _errors.Count > 0;
    public IReadOnlyList<Exception> Errors => _errors;

    public void Fail(string error)
    {
        try { throw new Exception(error); }
        catch (Exception ex) { _errors.Add(ex); }
    }

    public void Fail(Exception exception) => _errors.Add(exception);
}
