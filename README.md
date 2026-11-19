# MapLean

MapLean copies public members of the same name into a partial method.

## Install

```bash
dotnet add package MapLean
```

## Map a type

Put `[To]` on a partial method. The method's class is partial. The method takes the source and returns the destination. MapLean writes the method body. Public members with the same name are copied. Nested values, nullable values, and `List<T>` are mapped.

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

Version pending. Commit pending.

## Diagnostics

### ML0001

Move `[To]` onto a partial method. That method takes the source and returns the destination.

### ML0002

Add `partial` to the method.

### ML0003

Give the method one source parameter and a destination return type.

### ML0004

Change the member so MapLean can copy it. MapLean copies matching types, nested objects, nullable objects, and `List<T>`.

### ML0005

Add a public member of that name on the source, or remove `required` from the destination member.

### ML0006

The destination member has no matching source member. Add a public member with that name on the source, or mark the destination member with `[Leave]` to keep its value.

### ML0007

Change the declaring type to a partial class. MapLean writes a partial class.

### ML0008

Move the declaring type out of the outer type. MapLean writes a top-level partial class.

### ML0009

Make the type or the method public or internal. MapLean repeats that accessibility on the generated partial.
