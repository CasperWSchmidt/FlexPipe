# FlexPipe

A lightweight, strongly-typed pipeline framework targeting .NET Standard 2.0+. Define pipelines as a sequence of tasks, compose cross-cutting concerns with middleware, and let a Roslyn source generator wire up the context boilerplate automatically.

## Features

- **Strongly typed** — every pipeline is bound to a concrete `TInput` and `TOutput`
- **Failure channel** — tasks signal failure through the context rather than throwing; exceptions are caught automatically
- **Middleware** — decorate execution with logging, timing, auditing, transactions, or any cross-cutting concern
- **Source generator** — implements `IPipelineContext` for you; just declare the pipeline and extend the partial class if needed
- **Fluent builder** — register and compose pipelines with a clean API in `Program.cs` or `Startup.cs`
- **Fail-fast** — duplicate tasks, duplicate middlewares, and duplicate pipeline registrations are detected at startup, not at runtime

## Installation

Always install `FlexPipe` directly — this is what activates the source generator:

```
dotnet add package FlexPipe
```

Then add the adapter for your DI container:

**Microsoft.Extensions.DependencyInjection**
```
dotnet add package FlexPipe.Extensions.DependencyInjection
```

**SimpleInjector**
```
dotnet add package FlexPipe.Extensions.SimpleInjector
```

> The source generator is bundled inside the `FlexPipe` package and runs automatically when it is a direct dependency. Installing only an adapter package is not sufficient — NuGet does not activate analyzers from transitive dependencies.

If you are wiring up your own container, `FlexPipe` alone is all you need. `PipelineBuilder.Build()` returns an `IReadOnlyDictionary<(Type Input, Type Output), IPipelineDescriptor>` — iterate it to register tasks, middlewares, and the executor using your container's API.

## Quick start

### 1. Define input and output

```csharp
public class PriceInput
{
    public required decimal UnitPrice { get; init; }
    public required int Quantity { get; init; }
}

public class PriceOutput
{
    public decimal Total { get; set; }
}
```

### 2. Declare the pipeline

Implement `IPipelineDefinition<TInput, TOutput>` — this is the trigger for the source generator:

```csharp
public class PricePipeline : IPipelineDefinition<PriceInput, PriceOutput> { }
```

The generator produces a `PriceContext` partial class that implements `IPipelineContext<PriceInput, PriceOutput>`. To add custom properties, extend the partial:

```csharp
public partial class PriceContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
```

> The context name is derived by stripping the `Pipeline` suffix and appending `Context`. `PricePipeline` → `PriceContext`, `OrderHandler` → `OrderHandlerContext`.

### 3. Write tasks

```csharp
public class CalculateTotalTask : IPipelineTask<PriceInput, PriceOutput>
{
    public Task Execute(IPipelineContext<PriceInput, PriceOutput> context, CancellationToken cancellationToken)
    {
        if (context.Input.UnitPrice < 0)
            context.Fail("Unit price cannot be negative.");
        else
            context.Output.Total = context.Input.UnitPrice * context.Input.Quantity;

        return Task.CompletedTask;
    }
}
```

### 4. Register

```csharp
using FlexPipe.Extensions.DependencyInjection;

services.AddFlexPipe(builder =>
{
    builder
        .AddPipeline<PriceInput, PriceOutput>()
        .AddTask<CalculateTotalTask>();
});
```

### 5. Execute

```csharp
var executor = serviceProvider.GetRequiredService<IPipelineExecutor<PriceInput, PriceOutput>>(); // Or get it as a constructor argument

var context = new PriceContext
{
    Input = new PriceInput { UnitPrice = 9.99m, Quantity = 3 },
    CorrelationId = "abc-123"
};

var result = await executor.Execute(context);
Console.WriteLine(result.EnsureSuccess().Total); // 29.97
```

## Core concepts

### Tasks

A task is a single unit of work in a pipeline. Tasks run in registration order and stop as soon as the context enters a failed state.

```csharp
public class ValidateOrderTask : IPipelineTask<OrderInput, OrderOutput>
{
    public Task Execute(IPipelineContext<OrderInput, OrderOutput> context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Input.CustomerEmail))
            context.Fail("Customer email is required.");
        if (context.Input.Quantity <= 0)
            context.Fail("Quantity must be greater than zero.");

        return Task.CompletedTask;
    }
}
```

A task can call `Fail` multiple times within its own body — all errors are recorded before the pipeline stops.

### Failure channel

Tasks signal failure through `context.Fail()` rather than throwing. The pipeline context accumulates errors and exposes them on `PipelineResult<TOutput>` after execution. Unhandled exceptions thrown by tasks are automatically caught and converted to failures.

```csharp
void Fail(string error);        // wraps the message in an exception, stack trace points to the call site
void Fail(Exception exception); // stores the exception directly
```

```csharp
var result = await executor.Execute(context);

if (!result.HasSucceeded)
{
    foreach (var error in result.Errors)
        Console.WriteLine(error.Message);
}
```

### PipelineResult

`executor.Execute()` always returns a `PipelineResult<TOutput>` — it never throws unless the `CancellationToken` is cancelled.

```csharp
var result = await executor.Execute(context);

// Check manually
if (result.HasSucceeded)
    Console.WriteLine(result.Output!.OrderId);

// Or throw PipelineException on failure
var output = result.EnsureSuccess();
```

`PipelineException` aggregates all errors into its message and exposes them individually via `Errors`.

### Middleware

Middleware wraps the entire task sequence. It receives a `next` delegate and can execute code before and after the tasks run, or short-circuit execution entirely. Because middleware surrounds the task sequence, it always runs — making it the right place for cross-cutting concerns like auditing, logging, and timing.

```csharp
public class AuditMiddleware<TInput, TOutput> : IPipelineMiddleware<TInput, TOutput>
{
    public async Task Execute(
        IPipelineContext<TInput, TOutput> context,
        Func<Task> next,
        CancellationToken cancellationToken)
    {
        await next(); // tasks execute here

        // always runs — regardless of success or failure
        var status = context.IsFailed ? "failed" : "succeeded";
        Console.WriteLine($"[Audit] Pipeline {status}");
    }
}
```

Register middleware before tasks — it wraps execution in registration order (first registered = outermost):

```csharp
builder
    .AddPipeline<OrderInput, OrderOutput>()
    .AddMiddleware<AuditMiddleware<OrderInput, OrderOutput>>()
    .AddMiddleware<TimingMiddleware<OrderInput, OrderOutput>>()
    .AddTask<ValidateOrderTask>()
    .AddTask<CreateOrderTask>();
```

### Pipeline context

The source generator produces a `partial class` that implements `IPipelineContext<TInput, TOutput>`. The generated members are:

| Member | Description |
|---|---|
| `TInput Input { get; init; }` | Required at construction, immutable |
| `TOutput Output { get; set; }` | Written by tasks |
| `bool IsFailed` | True when at least one error has been recorded |
| `IReadOnlyList<Exception> Errors` | All recorded failures |
| `void Fail(string)` | Records a failure with a stack trace pointing to the call site |
| `void Fail(Exception)` | Records an existing exception directly |

Extend the partial to add your own properties:

```csharp
public partial class OrderContext
{
    public string CorrelationId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
```

When constructing the context, custom properties are set alongside the required `Input`:

```csharp
var context = new OrderContext
{
    Input = new OrderInput { CustomerEmail = "user@example.com", Sku = "ABC-1", Quantity = 2 },
    CorrelationId = "xyz-789"
};
```

## Builder API

### Task composition

```csharp
builder.AddPipeline<TInput, TOutput>()
    .AddTask<TaskA>()               // append to end
    .InsertFirst<TaskZ>()           // prepend to beginning
    .InsertLast<TaskY>()            // append to end (same as AddTask)
    .InsertBefore<TaskA, TaskX>()   // insert X immediately before A
    .InsertAfter<TaskA, TaskW>()    // insert W immediately after A
    .Remove<TaskY>()                // remove a specific task
    .RemoveAll();                   // clear all tasks
```

`InsertBefore` and `InsertAfter` throw `InvalidOperationException` at registration time if the target task is not found.

### Duplicate detection

Registering the same task or middleware type twice in a pipeline, or registering the same `TInput`/`TOutput` combination twice, throws `InvalidOperationException` at startup — not at runtime.

## Full example

```csharp
// Input / output
public class OrderInput
{
    public required string CustomerEmail { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
}

public class OrderOutput
{
    public Guid OrderId { get; set; }
}

// Pipeline definition (triggers source generator)
public class OrderPipeline : IPipelineDefinition<OrderInput, OrderOutput> { }

// Optional context extension
public partial class OrderContext
{
    public string CorrelationId { get; set; } = string.Empty;
}

// Middleware — always runs, wraps the full task sequence
public class AuditMiddleware<TInput, TOutput> : IPipelineMiddleware<TInput, TOutput>
{
    public async Task Execute(IPipelineContext<TInput, TOutput> context, Func<Task> next, CancellationToken ct)
    {
        await next();
        Console.WriteLine($"[Audit] {(context.IsFailed ? "Failed" : "Succeeded")}");
    }
}

// Tasks
public class ValidateOrderTask : IPipelineTask<OrderInput, OrderOutput>
{
    public Task Execute(IPipelineContext<OrderInput, OrderOutput> context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Input.CustomerEmail))
            context.Fail("Customer email is required.");
        if (context.Input.Quantity <= 0)
            context.Fail("Quantity must be greater than zero.");
        return Task.CompletedTask;
    }
}

public class CreateOrderTask : IPipelineTask<OrderInput, OrderOutput>
{
    public Task Execute(IPipelineContext<OrderInput, OrderOutput> context, CancellationToken cancellationToken)
    {
        context.Output.OrderId = Guid.NewGuid();
        return Task.CompletedTask;
    }
}

```

**Registration**

```csharp
using FlexPipe.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

services.AddFlexPipe(builder =>
{
    builder
        .AddPipeline<OrderInput, OrderOutput>()
        .AddMiddleware<AuditMiddleware<OrderInput, OrderOutput>>()
        .AddTask<ValidateOrderTask>()
        .AddTask<CreateOrderTask>();
});
```

**Execution**

```csharp
var executor = sp.GetRequiredService<IPipelineExecutor<OrderInput, OrderOutput>>();

var context = new OrderContext
{
    Input = new OrderInput { CustomerEmail = "user@example.com", Sku = "ABC-1", Quantity = 2 },
    CorrelationId = Guid.NewGuid().ToString()
};

var result = await executor.Execute(context);

if (result.HasSucceeded)
    Console.WriteLine($"Order: {result.Output!.OrderId}");
else
    Console.WriteLine($"Failed: {string.Join(", ", result.Errors.Select(e => e.Message))}");
```

## Project structure

```
src/
  FlexPipe/                                 Core library — interfaces, executor, builder
  FlexPipe.SourceGeneration/                Roslyn source generator for IPipelineContext
  FlexPipe.Extensions.DependencyInjection/  MS DI adapter
  FlexPipe.Extensions.SimpleInjector/       SimpleInjector adapter
  FlexPipe.Samples/                         Runnable examples
tests/
  FlexPipe.Tests/                           Unit and integration tests
  FlexPipe.SourceGeneration.Tests/          Source generator tests
```

## Requirements

- .NET Standard 2.0 compatible runtime (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- A DI adapter (`FlexPipe.Extensions.DependencyInjection`, `FlexPipe.Extensions.SimpleInjector`, or a custom one)
