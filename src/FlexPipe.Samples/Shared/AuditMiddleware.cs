namespace FlexPipe.Samples.Shared;

public class AuditMiddleware<TInput, TOutput> : IPipelineMiddleware<TInput, TOutput>
{
    public async Task Execute(
        IPipelineContext<TInput, TOutput> context,
        Func<Task> next,
        CancellationToken cancellationToken)
    {
        await next();

        var status = context.IsFailed ? "failed" : "succeeded";
        var errorSummary = context.IsFailed
            ? $" — {string.Join(", ", context.Errors.Select(e => e.Message))}"
            : string.Empty;

        Console.WriteLine($"[Audit] Pipeline {status}{errorSummary}");
    }
}
