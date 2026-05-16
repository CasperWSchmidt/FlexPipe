namespace FlexPipe.Tests.Helpers;

internal class FuncTask(
    Func<IPipelineContext<TestInput, TestOutput>, CancellationToken, Task>? execute = null)
    : IPipelineTask<TestInput, TestOutput>
{
    public Task Execute(IPipelineContext<TestInput, TestOutput> context, CancellationToken cancellationToken)
        => execute?.Invoke(context, cancellationToken) ?? Task.CompletedTask;
}

internal class FuncMiddleware(
    Func<IPipelineContext<TestInput, TestOutput>, Func<Task>, CancellationToken, Task> execute)
    : IPipelineMiddleware<TestInput, TestOutput>
{
    public Task Execute(
        IPipelineContext<TestInput, TestOutput> context,
        Func<Task> next,
        CancellationToken cancellationToken)
        => execute(context, next, cancellationToken);
}
