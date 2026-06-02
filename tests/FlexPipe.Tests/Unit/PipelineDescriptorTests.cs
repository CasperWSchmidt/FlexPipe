using FlexPipe.Tests.Helpers;
using Xunit;

namespace FlexPipe.Tests.Unit;

public class PipelineDescriptorTests
{
    [Fact]
    public void Build_DuplicateTask_ThrowsInvalidOperationException()
    {
        var builder = new PipelineBuilder();
        builder.GetOrAddPipeline<TestInput, TestOutput>()
            .AddTask<TaskA>()
            .AddTask<TaskA>();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("TaskA", ex.Message);
    }

    [Fact]
    public void Build_DuplicateMiddleware_ThrowsInvalidOperationException()
    {
        var builder = new PipelineBuilder();
        builder.GetOrAddPipeline<TestInput, TestOutput>()
            .AddMiddleware<MiddlewareA>()
            .AddMiddleware<MiddlewareA>();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("MiddlewareA", ex.Message);
    }

    [Fact]
    public void InsertBefore_NonExistentTask_ThrowsInvalidOperationException()
    {
        var descriptor = new PipelineBuilder().GetOrAddPipeline<TestInput, TestOutput>();
        descriptor.AddTask<TaskA>();

        var ex = Assert.Throws<InvalidOperationException>(() => descriptor.InsertBefore<TaskB, TaskA>());
        Assert.Contains("TaskB", ex.Message);
    }

    [Fact]
    public void InsertAfter_NonExistentTask_ThrowsInvalidOperationException()
    {
        var descriptor = new PipelineBuilder().GetOrAddPipeline<TestInput, TestOutput>();
        descriptor.AddTask<TaskA>();

        var ex = Assert.Throws<InvalidOperationException>(() => descriptor.InsertAfter<TaskB, TaskA>());
        Assert.Contains("TaskB", ex.Message);
    }

    [Fact]
    public void GetOrAddPipeline_CalledTwice_ReturnsSameInstance()
    {
        var builder = new PipelineBuilder();
        var first = builder.GetOrAddPipeline<TestInput, TestOutput>();
        var second = builder.GetOrAddPipeline<TestInput, TestOutput>();
        Assert.Same(first, second);
    }

    [Fact]
    public void Build_ReturnsDescriptor_WithCorrectTaskTypes()
    {
        var builder = new PipelineBuilder();
        builder.GetOrAddPipeline<TestInput, TestOutput>()
            .AddTask<TaskA>()
            .AddTask<TaskB>();

        var descriptor = builder.Build()[(typeof(TestInput), typeof(TestOutput))];

        Assert.Equal([typeof(TaskA), typeof(TaskB)], descriptor.Tasks);
    }

    [Fact]
    public void Build_ReturnsDescriptor_WithCorrectMiddlewareTypes()
    {
        var builder = new PipelineBuilder();
        builder.GetOrAddPipeline<TestInput, TestOutput>()
            .AddMiddleware<MiddlewareA>();

        var descriptor = builder.Build()[(typeof(TestInput), typeof(TestOutput))];

        Assert.Equal([typeof(MiddlewareA)], descriptor.Middlewares);
    }

    [Fact]
    public void Build_ReturnsEntry_ForEachRegisteredPipeline()
    {
        var builder = new PipelineBuilder();
        builder.GetOrAddPipeline<TestInput, TestOutput>();
        builder.GetOrAddPipeline<TestOutput, TestInput>();

        var result = builder.Build();

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey((typeof(TestInput), typeof(TestOutput))));
        Assert.True(result.ContainsKey((typeof(TestOutput), typeof(TestInput))));
    }

    [Fact]
    public void Build_ReturnsEmptyTasksAndMiddlewares_ForEmptyPipeline()
    {
        var builder = new PipelineBuilder();
        builder.GetOrAddPipeline<TestInput, TestOutput>();

        var descriptor = builder.Build()[(typeof(TestInput), typeof(TestOutput))];

        Assert.Empty(descriptor.Tasks);
        Assert.Empty(descriptor.Middlewares);
    }

    class TaskA : IPipelineTask<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, CancellationToken ct) => Task.CompletedTask;
    }

    class TaskB : IPipelineTask<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, CancellationToken ct) => Task.CompletedTask;
    }

    class MiddlewareA : IPipelineMiddleware<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, Func<Task> next, CancellationToken ct) => next();
    }
}
