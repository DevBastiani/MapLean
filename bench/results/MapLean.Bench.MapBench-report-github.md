```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26300.9974)
Unknown processor
.NET SDK 11.0.100
  [Host]     : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2


```
| Method     | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Manual     |  57.59 ns | 0.578 ns | 0.512 ns |  1.00 |    0.01 | 0.0482 |     504 B |        1.00 |
| MapLean    |  58.08 ns | 0.517 ns | 0.458 ns |  1.01 |    0.01 | 0.0482 |     504 B |        1.00 |
| AutoMapper | 121.86 ns | 0.479 ns | 0.425 ns |  2.12 |    0.02 | 0.0534 |     560 B |        1.11 |
