using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;

namespace EfCore.BulkOperations.Benchmark;

/// <summary>
///     Every operation here writes to a database, so the default throughput engine does not apply:
///     an operation cannot be repeated inside one iteration without changing the data it works on.
///     RunStrategy.Monitoring runs each iteration exactly once and honours [IterationSetup] and
///     [IterationCleanup], so each measurement starts from the same state.
/// </summary>
public class BenchmarksConfig : ManualConfig
{
    private static int IterationCount =>
        int.TryParse(Environment.GetEnvironmentVariable("BENCHMARK_ITERATIONS"), out var value)
            ? value
            : 10;

    public BenchmarksConfig()
    {
        AddJob(Job.Default
            .WithStrategy(RunStrategy.Monitoring)
            .WithLaunchCount(1)
            .WithWarmupCount(1)
            .WithIterationCount(IterationCount)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddLogger(ConsoleLogger.Default);
        AddColumn(TargetMethodColumn.Method, StatisticColumn.Median, StatisticColumn.StdDev);
        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.Declared));
    }
}
