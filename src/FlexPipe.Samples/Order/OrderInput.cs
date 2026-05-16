namespace FlexPipe.Samples.Order;

public class OrderInput
{
    public required string CustomerEmail { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
}
