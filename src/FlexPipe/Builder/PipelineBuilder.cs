namespace FlexPipe;

public class PipelineBuilder
{
    private readonly Dictionary<(Type Input, Type Output), IPipelineDescriptor> _descriptors = [];

    public PipelineDescriptor<TInput, TOutput> GetOrAddPipeline<TInput, TOutput>()
    {
        var key = (typeof(TInput), typeof(TOutput));
        if (_descriptors.TryGetValue(key, out var existing))
            return (PipelineDescriptor<TInput, TOutput>)existing;

        var descriptor = new PipelineDescriptor<TInput, TOutput>();
        _descriptors[key] = descriptor;
        return descriptor;
    }

    public IReadOnlyDictionary<(Type Input, Type Output), IPipelineDescriptor> Build()
    {
        foreach (var descriptor in _descriptors.Values)
            descriptor.Validate();

        return _descriptors;
    }
}
