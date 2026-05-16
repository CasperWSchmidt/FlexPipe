using FlexPipe;
using Microsoft.Extensions.DependencyInjection;

namespace FlexPipe.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlexPipe(
        this IServiceCollection services,
        Action<PipelineBuilder> configure)
    {
        var builder = new PipelineBuilder();
        configure(builder);

        foreach (var kvp in builder.Build())
        {
            var input = kvp.Key.Input;
            var output = kvp.Key.Output;
            var descriptor = kvp.Value;
            var taskServiceType = typeof(IPipelineTask<,>).MakeGenericType(input, output);
            var middlewareServiceType = typeof(IPipelineMiddleware<,>).MakeGenericType(input, output);

            foreach (var task in descriptor.Tasks)
                services.AddScoped(taskServiceType, task);

            foreach (var middleware in descriptor.Middlewares)
                services.AddScoped(middlewareServiceType, middleware);

            services.AddScoped(
                typeof(IPipelineExecutor<,>).MakeGenericType(input, output),
                typeof(PipelineExecutor<,>).MakeGenericType(input, output));
        }

        return services;
    }
}
