namespace FlexPipe.Samples.Order;

public class OrderContext : PipelineContext<OrderInput, OrderOutput>
{
    public string CorrelationId { get; set; } = string.Empty;
}
