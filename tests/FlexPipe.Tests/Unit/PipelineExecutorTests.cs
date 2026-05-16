using FlexPipe.Tests.Helpers;
using Xunit;

namespace FlexPipe.Tests.Unit;

public class PipelineExecutorTests
{
    static PipelineExecutor<TestInput, TestOutput> CreateExecutor(
        IEnumerable<IPipelineTask<TestInput, TestOutput>> tasks,
        IEnumerable<IPipelineMiddleware<TestInput, TestOutput>>? middlewares = null)
        => new(tasks, middlewares ?? []);

    static TestContext<TestInput, TestOutput> CreateContext()
        => new(new TestInput());

    [Fact]
    public async Task Execute_RunsTasks_InOrder()
    {
        var log = new List<string>();
        var executor = CreateExecutor([
            new FuncTask((_, _) => { log.Add("A"); return Task.CompletedTask; }),
            new FuncTask((_, _) => { log.Add("B"); return Task.CompletedTask; }),
            new FuncTask((_, _) => { log.Add("C"); return Task.CompletedTask; }),
        ]);

        await executor.Execute(CreateContext());

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task Execute_StopsAfterTaskFails()
    {
        var log = new List<string>();
        var executor = CreateExecutor([
            new FuncTask((ctx, _) => { ctx.Fail("error"); log.Add("A"); return Task.CompletedTask; }),
            new FuncTask((_, _) => { log.Add("B"); return Task.CompletedTask; }),
        ]);

        await executor.Execute(CreateContext());

        Assert.Equal(["A"], log);
    }

    [Fact]
    public async Task Execute_CapturesException_AsFailure()
    {
        var executor = CreateExecutor([
            new FuncTask((_, _) => throw new InvalidOperationException("boom")),
        ]);

        var result = await executor.Execute(CreateContext());

        Assert.False(result.HasSucceeded);
        Assert.Single(result.Errors);
        Assert.IsType<InvalidOperationException>(result.Errors[0]);
        Assert.Equal("boom", result.Errors[0].Message);
    }

    [Fact]
    public async Task Execute_ThrowsOperationCanceledException_WhenCancelled()
    {
        var cts = new CancellationTokenSource();
        var executor = CreateExecutor([
            new FuncTask((_, _) => { cts.Cancel(); return Task.CompletedTask; }),
            new FuncTask((_, _) => Task.CompletedTask),
        ]);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => executor.Execute(CreateContext(), cts.Token));
    }

    [Fact]
    public async Task Execute_RunsMiddleware_AroundTasks()
    {
        var log = new List<string>();
        var executor = CreateExecutor(
            tasks: [new FuncTask((_, _) => { log.Add("task"); return Task.CompletedTask; })],
            middlewares: [
                new FuncMiddleware(async (_, next, _) =>
                {
                    log.Add("before");
                    await next();
                    log.Add("after");
                }),
            ]);

        await executor.Execute(CreateContext());

        Assert.Equal(["before", "task", "after"], log);
    }

    [Fact]
    public async Task Execute_ChainsMiddlewares_OuterToInner()
    {
        var log = new List<string>();
        var executor = CreateExecutor(
            tasks: [new FuncTask((_, _) => { log.Add("task"); return Task.CompletedTask; })],
            middlewares: [
                new FuncMiddleware(async (_, next, _) => { log.Add("outer-in"); await next(); log.Add("outer-out"); }),
                new FuncMiddleware(async (_, next, _) => { log.Add("inner-in"); await next(); log.Add("inner-out"); }),
            ]);

        await executor.Execute(CreateContext());

        Assert.Equal(["outer-in", "inner-in", "task", "inner-out", "outer-out"], log);
    }

    [Fact]
    public async Task Execute_ReturnsSuccessResult_WithOutput()
    {
        var executor = CreateExecutor([
            new FuncTask((ctx, _) => { ctx.Output = new TestOutput { Value = "result" }; return Task.CompletedTask; }),
        ]);

        var result = await executor.Execute(CreateContext());

        Assert.True(result.HasSucceeded);
        Assert.Equal("result", result.Output!.Value);
    }

    [Fact]
    public async Task EnsureSuccess_ReturnsOutput_WhenSucceeded()
    {
        var executor = CreateExecutor([
            new FuncTask((ctx, _) => { ctx.Output = new TestOutput { Value = "result" }; return Task.CompletedTask; }),
        ]);

        var output = (await executor.Execute(CreateContext())).EnsureSuccess();

        Assert.Equal("result", output.Value);
    }

    [Fact]
    public async Task EnsureSuccess_ThrowsPipelineException_WhenFailed()
    {
        var executor = CreateExecutor([
            new FuncTask((ctx, _) => { ctx.Fail("e1"); ctx.Fail("e2"); return Task.CompletedTask; }),
        ]);

        var result = await executor.Execute(CreateContext());

        var ex = Assert.Throws<PipelineException>(() => result.EnsureSuccess());
        Assert.Equal(2, ex.Errors.Count);
        Assert.Contains("e1", ex.Message);
        Assert.Contains("e2", ex.Message);
    }
}
