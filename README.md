# MapLean

## Benchmark

Version 0.0.1. Commit af1a33631d718aa6cd26025116c5794a95197a7e.

```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor, 1 CPU, 4 logical and 4 physical cores
.NET SDK 8.0.428
  [Host]     : .NET 8.0.34 (8.0.3426.52217), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 8.0.34 (8.0.3426.52217), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```

| Method | Mean | Error | Ratio | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Manual | 141.4 ns | 2.73 ns | 1.00 | 504 B |
| MapLean | 135.0 ns | 2.72 ns | 0.96 | 504 B |
| AutoMapper | 274.6 ns | 3.92 ns | 1.94 | 560 B |
