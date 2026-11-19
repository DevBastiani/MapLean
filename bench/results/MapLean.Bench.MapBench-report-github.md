```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor, 1 CPU, 4 logical and 4 physical cores
.NET SDK 8.0.428
  [Host]     : .NET 8.0.34 (8.0.3426.52217), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 8.0.34 (8.0.3426.52217), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```
| Method     | Mean     | Error   | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------- |---------:|--------:|---------:|------:|--------:|-------:|----------:|------------:|
| Manual     | 127.3 ns | 2.51 ns |  3.26 ns |  1.00 |    0.04 | 0.0029 |     504 B |        1.00 |
| MapLean    | 121.1 ns | 1.59 ns |  1.41 ns |  0.95 |    0.03 | 0.0029 |     504 B |        1.00 |
| AutoMapper | 250.4 ns | 5.06 ns | 11.32 ns |  1.97 |    0.10 | 0.0033 |     560 B |        1.11 |
