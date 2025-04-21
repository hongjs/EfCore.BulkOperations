using BenchmarkDotNet.Running;
using EfCore.BulkOperations.Benchmark;

// BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

BenchmarkRunner.Run(typeof(BulkInsertTest));
// BenchmarkRunner.Run(typeof(BulkUpdateTest));
// BenchmarkRunner.Run(typeof(BulkDeleteTest));
// BenchmarkRunner.Run(typeof(BulkInsertTestContainer));
// BenchmarkRunner.Run(typeof(BatchSizeTest));