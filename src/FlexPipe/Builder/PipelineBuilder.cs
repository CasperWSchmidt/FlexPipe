namespace FlexPipe;

public class PipelineBuilder
{
    private readonly Dictionary<(Type Input, Type Output), IPipelineDescriptor> _descriptors = [];

    public PipelineDescriptor<TInput, TOutput> AddPipeline<TInput, TOutput>()
    {
        var descriptor = new PipelineDescriptor<TInput, TOutput>();

        var key = (typeof(TInput), typeof(TOutput));
        if (_descriptors.ContainsKey(key))
            throw new InvalidOperationException(
                $"A pipeline for '{typeof(TInput).Name}/{typeof(TOutput).Name}' is already registered.");
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
