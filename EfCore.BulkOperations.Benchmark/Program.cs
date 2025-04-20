using BenchmarkDotNet.Running;
using EfCore.BulkOperations.Benchmark;

// BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

var summary = BenchmarkRunner.Run(typeof(BulkInsert2));