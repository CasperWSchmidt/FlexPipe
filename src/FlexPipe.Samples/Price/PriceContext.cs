namespace FlexPipe.Samples.Price;

public class PriceContext : PipelineContext<PriceInput, PriceOutput>
{
    public string CorrelationId { get; set; } = string.Empty;
}
