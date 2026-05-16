namespace FlexPipe;

public interface IPipelineDescriptor
{
    IReadOnlyList<Type> Tasks { get; }
    IReadOnlyList<Type> Middlewares { get; }
    void Validate();
}

public class PipelineDescriptor<TInput, TOutput> : IPipelineDescriptor
{
    private readonly List<Type> _tasks = [];
    private readonly List<Type> _middlewares = [];

    IReadOnlyList<Type> IPipelineDescriptor.Tasks => _tasks;
    IReadOnlyList<Type> IPipelineDescriptor.Middlewares => _middlewares;

    public PipelineDescriptor<TInput, TOutput> AddTask<TTask>()
        where TTask : IPipelineTask<TInput, TOutput>
    {
        _tasks.Add(typeof(TTask));
        return this;
    }

    public PipelineDescriptor<TInput, TOutput> AddMiddleware<TMiddleware>()
        where TMiddleware : IPipelineMiddleware<TInput, TOutput>
    {
        _middlewares.Add(typeof(TMiddleware));
        return this;
    }

    public PipelineDescriptor<TInput, TOutput> InsertFirst<TNew>()
        where TNew : IPipelineTask<TInput, TOutput>
    {
        _tasks.Insert(0, typeof(TNew));
        return this;
    }

    public PipelineDescriptor<TInput, TOutput> InsertLast<TNew>()
        where TNew : IPipelineTask<TInput, TOutput>
    {
        _tasks.Add(typeof(TNew));
        return this;
    }

    public PipelineDescriptor<TInput, TOutput> InsertBefore<TExisting, TNew>()
        where TNew : IPipelineTask<TInput, TOutput>
    {
        var index = _tasks.IndexOf(typeof(TExisting));

        if (index < 0)
            throw new InvalidOperationException(
                $"Task '{typeof(TExisting).Name}' not found in pipeline for '{typeof(TInput).Name}/{typeof(TOutput).Name}'.");

        _tasks.Insert(index, typeof(TNew));
        return this;
    }

    public PipelineDescriptor<TInput, TOutput> InsertAfter<TExisting, TNew>()
        where TNew : IPipelineTask<TInput, TOutput>
    {
        var index = _tasks.IndexOf(typeof(TExisting));

        if (index < 0)
            throw new InvalidOperationException(
                $"Task '{typeof(TExisting).Name}' not found in pipeline for '{typeof(TInput).Name}/{typeof(TOutput).Name}'.");

        _tasks.Insert(index + 1, typeof(TNew));
        return this;
    }

    public PipelineDescriptor<TInput, TOutput> Remove<TTask>()
        where TTask : IPipelineTask<TInput, TOutput>
    {
        _tasks.Remove(typeof(TTask));
        return this;
    }

    public PipelineDescriptor<TInput, TOutput> RemoveAll()
    {
        _tasks.Clear();
        return this;
    }

    void IPipelineDescriptor.Validate()
    {
        CheckNoDuplicates(_tasks, "task");
        CheckNoDuplicates(_middlewares, "middleware");
    }

    private static void CheckNoDuplicates(List<Type> types, string label)
    {
        var seen = new HashSet<Type>();

        foreach (var type in types)
            if (!seen.Add(type))
                throw new InvalidOperationException(
                    $"Duplicate {label} '{type.Name}' in pipeline for '{typeof(TInput).Name}/{typeof(TOutput).Name}'.");
    }
}
