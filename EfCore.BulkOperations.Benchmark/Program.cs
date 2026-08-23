using System.Reflection;
using BenchmarkDotNet.Running;

// Needs a MySQL instance. `docker compose up -d` starts one, or point the benchmarks elsewhere
// with BENCHMARK_MYSQL. Row counts and iteration count come from BENCHMARK_ROWS and
// BENCHMARK_ITERATIONS.
//
//   dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter '*BulkInsertTest*'
//   dotnet run -c Release --project EfCore.BulkOperations.Benchmark -- --filter '*Bulk*Test' --join
BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
