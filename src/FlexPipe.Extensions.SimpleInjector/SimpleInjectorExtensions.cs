using FlexPipe;
using SimpleInjector;

namespace FlexPipe.Extensions.SimpleInjector;

public static class SimpleInjectorExtensions
{
    public static Container AddFlexPipe(
        this Container container,
        Action<PipelineBuilder> configure,
        Lifestyle? lifestyle = null)
    {
        lifestyle ??= Lifestyle.Scoped;

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
