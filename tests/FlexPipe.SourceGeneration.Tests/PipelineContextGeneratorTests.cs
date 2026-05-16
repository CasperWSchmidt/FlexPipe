using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace FlexPipe.SourceGeneration.Tests;

public class PipelineContextGeneratorTests
{
    private static CSharpCompilation CreateCompilation(string source)
    {
        var trustedPaths = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)!
            .Split(Path.PathSeparator);

        var references = trustedPaths
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(IPipelineDefinition<,>).Assembly.Location))
            .ToList();

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static GeneratorDriverRunResult Run(string source)
    {
        var compilation = CreateCompilation(source);
        return CSharpGeneratorDriver
            .Create(new PipelineContextGenerator())
            .RunGenerators(compilation)
            .GetRunResult();
    }

    private static string RunAndGetContext(string source)
        => Run(source).GeneratedTrees
            .Single(t => !t.FilePath.EndsWith("FlexPipePolyfills.g.cs"))
            .ToString();

    private static string GetPolyfills(string source)
        => Run(source).GeneratedTrees
            .Single(t => t.FilePath.EndsWith("FlexPipePolyfills.g.cs"))
            .ToString();

    [Fact]
    public void Generator_ProducesPartialClass_ForIPipelineDefinition()
    {
        var source = """
            using FlexPipe;
            namespace MyApp;
            public class MyPipeline : IPipelineDefinition<string, string> { }
            """;

        Assert.Contains("public partial class MyContext", RunAndGetContext(source));
    }

    [Fact]
    public void Generator_StripsPipelineSuffix_WhenDerivingContextName()
    {
        var source = """
            using FlexPipe;
            namespace MyApp;
            public class AddProductPipeline : IPipelineDefinition<string, string> { }
            """;

        var generated = RunAndGetContext(source);

        Assert.Contains("public partial class AddProductContext", generated);
        Assert.DoesNotContain("AddProductPipelineContext", generated);
    }

    [Fact]
    public void Generator_AddsContextSuffix_WhenNoPipelineSuffix()
    {
        var source = """
            using FlexPipe;
            namespace MyApp;
            public class OrderHandler : IPipelineDefinition<string, string> { }
            """;

        Assert.Contains("public partial class OrderHandlerContext", RunAndGetContext(source));
    }

    [Fact]
    public void Generator_PreservesNamespace_InGeneratedClass()
    {
        var source = """
            using FlexPipe;
            namespace MyApp.Orders;
            public class OrderPipeline : IPipelineDefinition<string, string> { }
            """;

        Assert.Contains("namespace MyApp.Orders", RunAndGetContext(source));
    }

    [Fact]
    public void Generator_ImplementsIPipelineContext_WithCorrectTypeArguments()
    {
        var source = """
            using FlexPipe;
            namespace MyApp;
            public class MyPipeline : IPipelineDefinition<string, string> { }
            """;

        var generated = RunAndGetContext(source);

        Assert.Contains("IPipelineContext<string, string>", generated);
    }

    [Fact]
    public void Generator_IncludesBothFailOverloads()
    {
        var source = """
            using FlexPipe;
            namespace MyApp;
            public class MyPipeline : IPipelineDefinition<string, string> { }
            """;

        var generated = RunAndGetContext(source);

        Assert.Contains("public void Fail(string error)", generated);
        Assert.Contains("public void Fail(global::System.Exception exception)", generated);
    }

    [Fact]
    public void Generator_Fail_UsesExceptionDispatchInfo_ForStackTrace()
    {
        var source = """
            using FlexPipe;
            namespace MyApp;
            public class MyPipeline : IPipelineDefinition<string, string> { }
            """;

        var generated = RunAndGetContext(source);

        Assert.Contains("ExceptionDispatchInfo", generated);
        Assert.Contains("SetCurrentStackTrace", generated);
    }

    [Fact]
    public void Generator_ProducesMultipleContextClasses_ForMultipleDefinitions()
    {
        var source = """
            using FlexPipe;
            namespace MyApp;
            public class OrderPipeline : IPipelineDefinition<string, string> { }
            public class ProductPipeline : IPipelineDefinition<string, int> { }
            """;

        var result = Run(source);

        // polyfills file + 2 context files
        Assert.Equal(3, result.GeneratedTrees.Length);
        Assert.Equal(2, result.GeneratedTrees.Count(t => !t.FilePath.EndsWith("FlexPipePolyfills.g.cs")));
    }

    [Fact]
    public void Generator_AlwaysEmitsPolyfillsFile_EvenWithNoDefinitions()
    {
        var source = """
            namespace MyApp;
            public class SomeClass { }
            """;

        var result = Run(source);

        Assert.Single(result.GeneratedTrees);
        Assert.EndsWith("FlexPipePolyfills.g.cs", result.GeneratedTrees[0].FilePath);
    }

    [Fact]
    public void Generator_PolyfillsFile_ContainsIsExternalInit()
    {
        var source = """
            namespace MyApp;
            public class SomeClass { }
            """;

        var polyfills = GetPolyfills(source);

        Assert.Contains("IsExternalInit", polyfills);
        Assert.Contains("NET5_0_OR_GREATER", polyfills);
    }

    [Fact]
    public void Generator_PolyfillsFile_ContainsRequiredMemberPolyfills()
    {
        var source = """
            namespace MyApp;
            public class SomeClass { }
            """;

        var polyfills = GetPolyfills(source);

        Assert.Contains("RequiredMemberAttribute", polyfills);
        Assert.Contains("CompilerFeatureRequiredAttribute", polyfills);
        Assert.Contains("SetsRequiredMembersAttribute", polyfills);
        Assert.Contains("NET7_0_OR_GREATER", polyfills);
    }
}
