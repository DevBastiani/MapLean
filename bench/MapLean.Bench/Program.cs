using BenchmarkDotNet.Running;
using MapLean.Bench;

if (args.Contains("--check"))
{
    new MapBench().Check();
    Console.WriteLine("ok");
    return;
}

BenchmarkRunner.Run<MapBench>();
