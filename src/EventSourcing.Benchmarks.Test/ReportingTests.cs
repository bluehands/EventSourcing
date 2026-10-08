namespace EventSourcing.Benchmarks.Test;

[TestClass]
public class ReportingTests
{
    [TestMethod]
    public void MetricsExcludeFailuresFromThroughputAndSuccessfulLatencyPercentiles()
    {
        var samples = Enumerable.Range(1, 100).Select(i => new OperationSample("write", 0, i, i, 10, null))
            .Append(new("write", 0, 101, 10000, 0, "timeout"));
        var metrics = Metrics.Calculate(samples, 2);
        Assert.AreEqual(500d, metrics.EventsPerSecond);
        Assert.AreEqual(50d, metrics.OperationsPerSecond);
        Assert.AreEqual(50d, metrics.MedianMilliseconds);
        Assert.AreEqual(95d, metrics.P95Milliseconds);
        Assert.AreEqual(99d, metrics.P99Milliseconds);
        Assert.AreEqual(1, metrics.FailedOperations);
    }

    [TestMethod]
    public void MixedSummariesUseSeparateGroupDurationsAndRetainErrors()
    {
        var workload = new Workload("mixed", 100, 256, 10, 1, 1);
        Measurement[] measurements = [
            new("sqlite", workload, 1, 10, 2, 10, [new("read", 0, 0, 1, 100, null), new("write", 0, 0, 2, 10, null)]),
            new("sqlite", workload, 2, 5, 1, 5, [new("read", 0, 0, 1, 100, null), new("write", 0, 0, 3, 0, "busy")])
        ];
        var summaries = Report.Summarize(measurements);
        var read = summaries.Single(s => s.Kind == "read");
        var write = summaries.Single(s => s.Kind == "write");
        Assert.AreEqual(200d / 3, read.Metrics.EventsPerSecond, .00001);
        Assert.AreEqual(10d / 15, write.Metrics.EventsPerSecond, .00001);
        Assert.AreEqual(1, write.Errors["busy"]);
        Assert.AreEqual(1d, read.Scaling);
    }

    [TestMethod]
    public void ParallelScalingMatchesOnlyTheSameProviderAndWorkloadShape()
    {
        var single = new Workload("write", 100, 256, 10, 0, 1);
        var parallel = single with { Writers = 4 };
        Measurement[] measurements = [
            new("sqlite", single, 1, 2, 0, 2, [new("write", 0, 0, 1, 10, null)]),
            new("sqlite", parallel, 1, 4, 0, 4, [new("write", 0, 0, 1, 40, null)]),
            new("sqlserver", parallel, 1, 4, 0, 4, [new("write", 0, 0, 1, 40, null)]),
            new("sqlite", parallel with { BatchSize = 100 }, 1, 4, 0, 4, [new("write", 0, 0, 1, 400, null)])
        ];
        var summaries = Report.Summarize(measurements);
        Assert.AreEqual(2d, summaries.Single(s => s.Provider == "sqlite" && s.Workload == parallel).Scaling);
        Assert.IsNull(summaries.Single(s => s.Provider == "sqlserver").Scaling);
        Assert.IsNull(summaries.Single(s => s.Workload.BatchSize == 100).Scaling);
    }

    [TestMethod]
    public void MatrixOnlyVariesBatchSizeForWriteScenariosAndSupportsMixedOverrides()
    {
        var options = Options.Parse(["--profile", "smoke", "--scenarios", "stream,mixed", "--readers", "3", "--writers", "2"]);
        var workloads = options.Workloads().Distinct().ToArray();
        Assert.AreEqual(2, workloads.Count(w => w.Scenario == "stream"));
        Assert.AreEqual(2, workloads.Count(w => w.Scenario == "mixed"));
        Assert.IsTrue(workloads.Where(w => w.Scenario == "mixed").All(w => w.Readers == 3 && w.Writers == 2));
        Assert.Throws<ArgumentException>(() => Options.Parse(["--operations", "0"]));
    }
}
