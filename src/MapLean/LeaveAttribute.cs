using System.Diagnostics;

namespace MapLean;

[Conditional("MAPLEAN_USAGES")]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, Inherited = false)]
public sealed class LeaveAttribute : Attribute;
