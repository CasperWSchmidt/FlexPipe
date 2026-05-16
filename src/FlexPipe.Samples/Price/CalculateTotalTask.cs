namespace FlexPipe.Samples.Price;

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
