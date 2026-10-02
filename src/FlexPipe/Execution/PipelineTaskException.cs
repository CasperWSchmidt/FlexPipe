namespace FlexPipe;

/// <summary>
/// Represents a failure recorded by a task through <see cref="IPipelineContext{TInput, TOutput}.Fail(string)"/>.
/// </summary>
public class PipelineTaskException : Exception
{
    public PipelineTaskException(string message)
        : base(message)
    {
    }
}
