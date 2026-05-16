using FlexPipe.Extensions.DependencyInjection;
using FlexPipe.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlexPipe.Tests.Integration;

public class PipelineIntegrationTests
{
    static (IServiceProvider sp, List<string> log) BuildPipeline(
        Action<PipelineDescriptor<TestInput, TestOutput>> configure)
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddFlexPipe(builder => configure(builder.AddPipeline<TestInput, TestOutput>()));
        return (services.BuildServiceProvider(), log);
    }

    static TestContext<TestInput, TestOutput> CreateContext() => new(new TestInput());

    [Fact]
    public void AddFlexPipe_RegistersExecutor()
    {
        var services = new ServiceCollection();
        services.AddFlexPipe(builder =>
            builder.AddPipeline<TestInput, TestOutput>().AddTask<SimpleTask>());

        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IPipelineExecutor<TestInput, TestOutput>>());
    }

    [Fact]
    public async Task Pipeline_ExecutesTasks_InRegistrationOrder()
    {
        var (sp, log) = BuildPipeline(def => def
            .AddTask<TaskA>()
            .AddTask<TaskB>()
            .AddTask<TaskC>());

        await sp.GetRequiredService<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task InsertBefore_InsertsTask_BeforeExisting()
    {
        var (sp, log) = BuildPipeline(def => def
            .AddTask<TaskA>()
            .AddTask<TaskC>()
            .InsertBefore<TaskC, TaskB>());

        await sp.GetRequiredService<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task InsertAfter_InsertsTask_AfterExisting()
    {
        var (sp, log) = BuildPipeline(def => def
            .AddTask<TaskA>()
            .AddTask<TaskC>()
            .InsertAfter<TaskA, TaskB>());

        await sp.GetRequiredService<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task InsertFirst_InsertsTask_AtBeginning()
    {
        var (sp, log) = BuildPipeline(def => def
            .AddTask<TaskB>()
            .AddTask<TaskC>()
            .InsertFirst<TaskA>());

        await sp.GetRequiredService<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task InsertLast_InsertsTask_AtEnd()
    {
        var (sp, log) = BuildPipeline(def => def
            .AddTask<TaskA>()
            .AddTask<TaskB>()
            .InsertLast<TaskC>());

        await sp.GetRequiredService<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());

        Assert.Equal(["A", "B", "C"], log);
    }

    [Fact]
    public async Task Remove_RemovesTask_FromPipeline()
    {
        var (sp, log) = BuildPipeline(def => def
            .AddTask<TaskA>()
            .AddTask<TaskB>()
            .AddTask<TaskC>()
            .Remove<TaskB>());

        await sp.GetRequiredService<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());

        Assert.Equal(["A", "C"], log);
    }

    [Fact]
    public async Task RemoveAll_ClearsAllTasks()
    {
        var (sp, log) = BuildPipeline(def => def
            .AddTask<TaskA>()
            .AddTask<TaskB>()
            .RemoveAll());

        await sp.GetRequiredService<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());

        Assert.Empty(log);
    }

    class SimpleTask : IPipelineTask<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, CancellationToken ct) => Task.CompletedTask;
    }

    class TaskA(List<string> log) : IPipelineTask<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, CancellationToken ct)
        { log.Add("A"); return Task.CompletedTask; }
    }

    class TaskB(List<string> log) : IPipelineTask<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, CancellationToken ct)
        { log.Add("B"); return Task.CompletedTask; }
    }

    class TaskC(List<string> log) : IPipelineTask<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, CancellationToken ct)
        { log.Add("C"); return Task.CompletedTask; }
    }
}
