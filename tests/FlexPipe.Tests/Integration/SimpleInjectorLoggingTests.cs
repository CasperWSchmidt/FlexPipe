using FlexPipe.Extensions.SimpleInjector;
using FlexPipe.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleInjector;
using Xunit;

namespace FlexPipe.Tests.Integration;

public class SimpleInjectorLoggingTests
{
    static TestContext<TestInput, TestOutput> CreateContext() => new(new TestInput());

    // Pure-SimpleInjector host (no ServiceCollection bridge). The developer registers the
    // ILoggerFactory in the container themselves; FlexPipe never registers one.
    static async Task<CapturingLoggerFactory> RunPure(Action<FlexPipeOptions>? configureOptions)
    {
        var factory = new CapturingLoggerFactory();
        var container = new Container();
        container.RegisterInstance<ILoggerFactory>(factory);
        container.AddFlexPipe(
            b => b.GetOrAddPipeline<TestInput, TestOutput>().AddTask<NoopTask>(),
            Lifestyle.Transient,
            configureOptions);
        container.Verify();

        await container.GetInstance<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());
        return factory;
    }

    [Fact]
    public async Task PureSimpleInjector_WithExplicitFactory_CapturesPipelineLogs()
    {
        var factory = await RunPure(configureOptions: null);

        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Pipeline "));
    }

    [Fact]
    public async Task PureSimpleInjector_DefaultsToTaskLoggingEnabled()
    {
        var factory = await RunPure(configureOptions: null);

        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Task "));
    }

    [Fact]
    public async Task AddFlexPipe_AppliesConfiguredOptions_ToExecutor()
    {
        var factory = await RunPure(options => options.EnableTaskLogging = false);

        Assert.DoesNotContain(factory.Entries, e => e.Message.StartsWith("Task "));
        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Pipeline "));
    }

    [Fact]
    public async Task AddFlexPipe_UsesConfiguredLogCategory()
    {
        var factory = await RunPure(options => options.LogCategory = "MyApp.Pipelines");

        Assert.Contains("MyApp.Pipelines", factory.Categories);
    }

    [Fact]
    public async Task PureSimpleInjector_WithSelfRegisteredNullLoggerFactory_ResolvesAndExecutes()
    {
        var container = new Container();
        container.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
        container.AddFlexPipe(
            b => b.GetOrAddPipeline<TestInput, TestOutput>().AddTask<NoopTask>(),
            Lifestyle.Transient);
        container.Verify();

        var result = await container.GetInstance<IPipelineExecutor<TestInput, TestOutput>>()
            .Execute(CreateContext());

        Assert.True(result.HasSucceeded);
    }

    [Fact]
    public void PureSimpleInjector_WithoutFactory_LeavesLoggerFactoryUnregistered()
    {
        // Contract: FlexPipe never registers an ILoggerFactory. With none registered by the
        // developer and no cross-wiring set up, the executor's ILoggerFactory dependency is
        // unresolvable and Verify surfaces it — no silent no-op logging.
        var container = new Container();
        container.AddFlexPipe(
            b => b.GetOrAddPipeline<TestInput, TestOutput>().AddTask<NoopTask>(),
            Lifestyle.Transient);

        var ex = Assert.ThrowsAny<Exception>(() => container.Verify());
        Assert.Contains(nameof(ILoggerFactory), ex.ToString());
    }

    [Fact]
    public async Task BridgedHost_AutoCrossWiresHostLoggerFactory()
    {
        // ASP.NET Core-style: host registers logging in the ServiceCollection and bridges to
        // SimpleInjector. With no explicit factory, the host's ILoggerFactory is cross-wired
        // into the executor automatically — the same zero-glue experience as the MS DI adapter.
        var factory = new CapturingLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(factory);
        var container = new Container();
        services.AddSimpleInjector(container);

        container.AddFlexPipe(
            b => b.GetOrAddPipeline<TestInput, TestOutput>().AddTask<NoopTask>(),
            Lifestyle.Transient);

        var provider = services.BuildServiceProvider();
        provider.UseSimpleInjector(container);
        container.Verify();

        await container.GetInstance<IPipelineExecutor<TestInput, TestOutput>>().Execute(CreateContext());

        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Pipeline "));
        Assert.Contains(factory.Entries, e => e.Message.StartsWith("Task "));
    }

    class NoopTask : IPipelineTask<TestInput, TestOutput>
    {
        public Task Execute(IPipelineContext<TestInput, TestOutput> ctx, CancellationToken ct)
            => Task.CompletedTask;
    }
}
