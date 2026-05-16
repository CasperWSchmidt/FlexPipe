using System.Diagnostics;

namespace FlexPipe.Samples.Shared;

public class TimingMiddleware<TInput, TOutput> : IPipelineMiddleware<TInput, TOutput>
{
    public async Task Execute(
        IPipelineContext<TInput, TOutput> context,
        Func<Task> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await next();
        Console.WriteLine($"[Timing] Pipeline completed in {sw.ElapsedMilliseconds}ms");
    }
}
