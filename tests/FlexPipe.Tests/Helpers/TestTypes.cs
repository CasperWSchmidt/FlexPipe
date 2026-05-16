namespace FlexPipe.Tests.Helpers;

record TestInput(string Value = "");

class TestOutput
{
    public string Value { get; set; } = string.Empty;
}
