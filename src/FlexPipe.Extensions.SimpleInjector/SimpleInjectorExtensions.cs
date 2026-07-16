using FlexPipe;
using SimpleInjector;

namespace FlexPipe.Extensions.SimpleInjector;

public static class SimpleInjectorExtensions
{
    public static Container AddFlexPipe(
        this Container container,
        Action<PipelineBuilder> configure,
        Lifestyle? lifestyle = null,
        Action<FlexPipeOptions>? configureOptions = null)
    {
        lifestyle ??= Lifestyle.Scoped;

        var options = new FlexPipeOptions();
        configureOptions?.Invoke(options);
        container.RegisterInstance(options);

        // The executor takes an ILoggerFactory, which FlexPipe deliberately does not register
        // here. ILogger(Factory) is a host concern, normally set up via AddLogging on the
        // IServiceCollection. In an ASP.NET Core + SimpleInjector host
        // (SimpleInjector.Integration.ServiceCollection) it is auto cross-wired from the
        // framework — matching the MS DI adapter. A pure-SimpleInjector app registers its own
        // ILoggerFactory in the container (or NullLoggerFactory.Instance for no logging).

        var builder = new PipelineBuilder();
        configure(builder);

        foreach (var kvp in builder.Build())
        {
            var input = kvp.Key.Input;
            var output = kvp.Key.Output;
            var descriptor = kvp.Value;
            var taskServiceType = typeof(IPipelineTask<,>).MakeGenericType(input, output);
            var middlewareServiceType = typeof(IPipelineMiddleware<,>).MakeGenericType(input, output);

            container.Collection.Register(taskServiceType,
                descriptor.Tasks.Select(t => lifestyle.CreateRegistration(t, container)).ToArray());

            container.Collection.Register(middlewareServiceType,
                descriptor.Middlewares.Select(m => lifestyle.CreateRegistration(m, container)).ToArray());

            container.Register(
                typeof(IPipelineExecutor<,>).MakeGenericType(input, output),
                typeof(PipelineExecutor<,>).MakeGenericType(input, output),
                lifestyle);
        }

        return container;
    }
}
