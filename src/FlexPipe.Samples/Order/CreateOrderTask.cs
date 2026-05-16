namespace FlexPipe.Samples.Order;

public class CreateOrderTask : IPipelineTask<OrderInput, OrderOutput>
{
    public Task Execute(IPipelineContext<OrderInput, OrderOutput> context, CancellationToken cancellationToken)
    {
        context.Output.OrderId = Guid.NewGuid();
        return Task.CompletedTask;
    }
}
