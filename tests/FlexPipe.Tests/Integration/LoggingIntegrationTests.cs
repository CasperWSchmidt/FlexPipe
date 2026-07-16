using FlexPipe.Extensions.DependencyInjection;
using FlexPipe.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FlexPipe.Tests.Integration;

public class LoggingIntegrationTests
{
    static TestContext<TestInput, TestOutput> CreateContext() => new(new TestInput());

    static async Task<CapturingLoggerFactory> RunWith(
        Action<FlexPipeOptions>? configureOptions)
    {
        var factory = new CapturingLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(factory);
        services.AddFlexPipe(
            builder => builder.GetOrAddPipeline<TestInput, TestOutput>().AddTask<NoopTask>(),
            configureOptions);

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());
        return factory;
    }

    [Fact]
    public async Task AddFlexPipe_InjectsRegisteredLoggerFactory_IntoExecutor()
    {
        var factory = await RunWith(configureOptions: null);

        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Pipeline "));
    }

    [Fact]
    public async Task AddFlexPipe_DefaultsToTaskLoggingEnabled()
    {
        var factory = await RunWith(configureOptions: null);

        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Task "));
    }

    [Fact]
    public async Task AddFlexPipe_AppliesConfiguredOptions_ToExecutor()
    {
        var factory = await RunWith(options => options.EnableTaskLogging = false);

        Assert.DoesNotContain(factory.Entries, e => e.Message.StartsWith("Task "));
        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Pipeline "));
    }

    [Fact]
    public async Task AddFlexPipe_UsesConfiguredLogCategory()
    {
        var factory = await RunWith(options => options.LogCategory = "MyApp.Pipelines");

        Assert.Contains("MyApp.Pipelines", factory.Categories);
    }

    class NoopTask : IPipelineTask<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, CancellationToken ct)
            => Task.CompletedTask;
    }
}
