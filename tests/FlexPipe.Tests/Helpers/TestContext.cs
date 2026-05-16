using System.Runtime.ExceptionServices;

namespace FlexPipe.Tests.Helpers;

internal class TestContext<TInput, TOutput>(TInput input) : IPipelineContext<TInput, TOutput>
{
    private readonly List<Exception> _errors = [];

    public TInput Input { get; } = input;
    public TOutput Output { get; set; } = default!;
    public bool IsFailed => _errors.Count > 0;
    public IReadOnlyList<Exception> Errors => _errors;

    public void Fail(string error)
    {
        var ex = new Exception(error);
        ExceptionDispatchInfo.SetCurrentStackTrace(ex);
        _errors.Add(ex);
    }

    public void Fail(Exception exception) => _errors.Add(exception);
}
