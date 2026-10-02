using Xunit;

namespace FlexPipe.Tests.Unit;

public class PipelineContextTests
{
    [Fact]
    public void Fail_WithMessage_RecordsPipelineTaskException()
    {
        var context = new PipelineContext<string, string> { Input = "input" };

        context.Fail("something broke");

        var ex = Assert.IsType<PipelineTaskException>(Assert.Single(context.Errors));
        Assert.Equal("something broke", ex.Message);
        Assert.Contains(nameof(Fail_WithMessage_RecordsPipelineTaskException), ex.StackTrace);
    }
}
