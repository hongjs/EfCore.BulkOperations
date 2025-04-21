using BenchmarkDotNet.Running;
using EfCore.BulkOperations.Benchmark;

// BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

// BenchmarkRunner.Run(typeof(BulkInsert));
BenchmarkRunner.Run(typeof(BulkUpdate));
// BenchmarkRunner.Run(typeof(BulkDelete));
// BenchmarkRunner.Run(typeof(BulkInsertTestContainer));