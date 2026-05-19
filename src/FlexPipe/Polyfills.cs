#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif

#if !NET7_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(
        global::System.AttributeTargets.Class |
        global::System.AttributeTargets.Struct |
        global::System.AttributeTargets.Field |
        global::System.AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class RequiredMemberAttribute : global::System.Attribute { }

    [global::System.AttributeUsage(
        global::System.AttributeTargets.All,
        AllowMultiple = true,
        Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : global::System.Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }
        public bool IsOptional { get; set; }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [global::System.AttributeUsage(
        global::System.AttributeTargets.Constructor,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : global::System.Attribute { }
}
#endif
