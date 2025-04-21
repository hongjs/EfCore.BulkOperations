using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Loggers;

namespace EfCore.BulkOperations.Benchmark;

public class BenchmarksConfig : ManualConfig
{
    public BenchmarksConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        AddLogger(ConsoleLogger.Default);
        AddColumn(TargetMethodColumn.Method, StatisticColumn.Median, StatisticColumn.StdDev);
    }
}