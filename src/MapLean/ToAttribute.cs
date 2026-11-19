using System.Diagnostics;

namespace MapLean;

[Conditional("MAPLEAN_USAGES")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ToAttribute : Attribute;
