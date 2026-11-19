# MapLean

MapLean copies public members of the same name into a partial method.

## Install

```bash
dotnet add package MapLean
```

The package adds no runtime dependency: the generator runs at build time and `MapLean.dll` is a compile-time reference only.

## Map a type

Put `[To]` on a partial method. The method's class is partial. The method takes the source and returns the destination. MapLean writes the method body. Public members with the same name are copied. Nested values, nullable values, and `List<T>` are mapped. A member of the same type is assigned, not cloned, so the source and the destination share that reference, arrays and nested objects of that type included.

```csharp
using MapLean;

public static partial class PersonMap
{
    [To]
    public static partial PersonDto Apply(Person source);
}

public sealed record Person(string Name);
public sealed record PersonDto(string Name);
```

A destination member with no source keeps its value. MapLean reports [ML0006](#ml0006) unless you mark the member with `[Leave]`. A source member with no destination is left out.

```csharp
public sealed record PersonDto(string Name)
{
    [Leave]
    public string Channel { get; init; } = "unset";
}
```

The method's type is a top-level partial class, and that class and method are public or internal. MapLean reports [ML0007](#ml0007), [ML0008](#ml0008), or [ML0009](#ml0009) for the other shapes.

## Benchmark

Version 0.0.1. Commit 145b806f28512884d0bbf2affc20f4ed2df36417.

MapLean is the same speed as the hand-written map and about 2x faster than AutoMapper.

```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26300.9974)
Unknown processor
.NET SDK 11.0.100
  [Host]     : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2


```

| Method | Mean | Error | Ratio | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Manual | 57.59 ns | 0.578 ns | 1.00 | 504 B |
| MapLean | 58.08 ns | 0.517 ns | 1.01 | 504 B |
| AutoMapper | 121.86 ns | 0.479 ns | 2.12 | 560 B |

## Diagnostics

### ML0001

Move `[To]` onto a partial method. That method takes the source and returns the destination.

### ML0002

Add `partial` to the method.

### ML0003

Give the method one source parameter, passed by value or with `in`, and a destination return type. `ref` and `out` are not supported.

### ML0004

Change the member so MapLean can copy it. MapLean copies matching types, nested objects, nullable objects, nullable structs, and `List<T>`. A nullable member does not map to a non-nullable one, and other collections are not mapped.

### ML0005

Add a public member of that name on the source, or remove `required` from the destination member.

### ML0006

The destination member has no matching source member. Add a public member with that name on the source, or mark the destination member with `[Leave]` to keep its value.

### ML0007

Change the declaring type to a partial class, and add `partial` if the class lacks it. MapLean writes a partial class.

### ML0008

Move the declaring type out of the outer type. MapLean writes a top-level partial class.

### ML0009

Make the type or the method public or internal. MapLean repeats that accessibility on the generated partial.

### ML0010

Mark the destination member with `[Leave]` and set it yourself. The type refers back to itself, directly or through another type or a `List<T>`, and MapLean does not map recursive types.

### ML0011

A constructor parameter has no default value and MapLean has no source for it. Add a public member with that name on the source, or give the parameter a default value. A parameter marked `[Leave]` needs a default value too, so the generated call can leave it out.
