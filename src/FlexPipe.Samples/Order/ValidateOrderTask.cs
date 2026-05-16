namespace FlexPipe.Samples.Order;

public class ValidateOrderTask : IPipelineTask<OrderInput, OrderOutput>
{
    public Task Execute(IPipelineContext<OrderInput, OrderOutput> context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Input.CustomerEmail))
            context.Fail("Customer email is required.");
        if (string.IsNullOrWhiteSpace(context.Input.Sku))
            context.Fail("SKU is required.");
        if (context.Input.Quantity <= 0)
            context.Fail("Quantity must be greater than zero.");

        return Task.CompletedTask;
    }
}
