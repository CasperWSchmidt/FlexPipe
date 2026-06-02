using FlexPipe;
using FlexPipe.Extensions.DependencyInjection;
using FlexPipe.Samples.Order;
using FlexPipe.Samples.Price;
using FlexPipe.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;

// ---- Sample 1: Price calculation with timing middleware ----
Console.WriteLine("=== Sample 1: Price Calculation ===");

var services = new ServiceCollection();
services.AddFlexPipe(builder =>
{
    builder
        .GetOrAddPipeline<PriceInput, PriceOutput>()
        .AddMiddleware<TimingMiddleware<PriceInput, PriceOutput>>()
        .AddTask<CalculateTotalTask>();
});

var sp = services.BuildServiceProvider();
var priceExecutor = sp.GetRequiredService<IPipelineExecutor<PriceInput, PriceOutput>>();

var priceContext = new PriceContext
{
    Input = new PriceInput { UnitPrice = 9.99m, Quantity = 3 },
    CorrelationId = Guid.NewGuid().ToString()
};

var priceResult = await priceExecutor.Execute(priceContext);
Console.WriteLine($"Total: {priceResult.EnsureSuccess().Total:C}");
Console.WriteLine();

// ---- Sample 2: Order pipeline with audit middleware ----
// AuditMiddleware always runs after tasks, regardless of success or failure.
Console.WriteLine("=== Sample 2: Order Validation Failure ===");

services = new ServiceCollection();
services.AddFlexPipe(builder =>
{
    builder
        .GetOrAddPipeline<OrderInput, OrderOutput>()
        .AddMiddleware<AuditMiddleware<OrderInput, OrderOutput>>()
        .AddTask<ValidateOrderTask>()
        .AddTask<CreateOrderTask>();
});

sp = services.BuildServiceProvider();
var orderExecutor = sp.GetRequiredService<IPipelineExecutor<OrderInput, OrderOutput>>();

var invalidContext = new OrderContext
{
    Input = new OrderInput { CustomerEmail = "", Sku = "ABC-123", Quantity = 2 },
    CorrelationId = Guid.NewGuid().ToString()
};

var invalidResult = await orderExecutor.Execute(invalidContext);
Console.WriteLine($"Errors: {string.Join(", ", invalidResult.Errors.Select(e => e.Message))}");
Console.WriteLine();

// ---- Sample 3: Successful order using EnsureSuccess ----
Console.WriteLine("=== Sample 3: Successful Order ===");

var validContext = new OrderContext
{
    Input = new OrderInput { CustomerEmail = "customer@example.com", Sku = "ABC-123", Quantity = 2 },
    CorrelationId = Guid.NewGuid().ToString()
};

var validResult = await orderExecutor.Execute(validContext);
Console.WriteLine($"Order ID: {validResult.EnsureSuccess().OrderId}");
