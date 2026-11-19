```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor, 1 CPU, 4 logical and 4 physical cores
.NET SDK 8.0.428
  [Host]     : .NET 8.0.34 (8.0.3426.52217), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 8.0.34 (8.0.3426.52217), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```
| Method     | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| Manual     | 141.4 ns | 2.73 ns | 2.68 ns |  1.00 |    0.03 | 0.0029 |     504 B |        1.00 |
| MapLean    | 135.0 ns | 2.72 ns | 5.05 ns |  0.96 |    0.04 | 0.0029 |     504 B |        1.00 |
| AutoMapper | 274.6 ns | 3.92 ns | 4.02 ns |  1.94 |    0.05 | 0.0033 |     560 B |        1.11 |
