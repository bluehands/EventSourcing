using AwesomeAssertions;

namespace EventSourcing.Benchmarks.Test;

public class ReportingTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    public void SingleProviderSelectionDoesNotIncludeOtherProviders(string provider)
    {
        (new Options { Provider = provider }).Providers().Should().Equal(provider);
    }

    [Fact]
    public void AllSelectsEveryProviderAndBothIsRejected()
    {
        (new Options { Provider = "all" }).Providers().Should().Equal("sqlite", "sqlserver", "postgres");
        Action parse = () => Options.Parse(["--provider", "both"]);
        parse.Should().ThrowExactly<ArgumentException>().WithMessage("Provider must be sqlite, sqlserver, postgres, or all.");
    }

    [Fact]
    public void MetricsExcludeFailuresFromThroughputAndSuccessfulLatencyPercentiles()
    {
        var samples = Enumerable.Range(1, 100).Select(i => new OperationSample("write", 0, i, i, 10, null))
            .Append(new("write", 0, 101, 10000, 0, "timeout"));
        var metrics = Metrics.Calculate(samples, 2);
        metrics.EventsPerSecond.Should().Be(500d);
        metrics.OperationsPerSecond.Should().Be(50d);
        metrics.MedianMilliseconds.Should().Be(50d);
        metrics.P95Milliseconds.Should().Be(95d);
        metrics.P99Milliseconds.Should().Be(99d);
        metrics.FailedOperations.Should().Be(1);
    }

    [Fact]
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
        read.Metrics.EventsPerSecond.Should().BeApproximately(200d / 3, .00001);
        write.Metrics.EventsPerSecond.Should().BeApproximately(10d / 15, .00001);
        write.Errors["busy"].Should().Be(1);
        read.Scaling.Should().Be(1d);
    }

    [Fact]
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
        summaries.Single(s => s.Provider == "sqlite" && s.Workload == parallel).Scaling.Should().Be(2d);
        summaries.Single(s => s.Provider == "sqlserver").Scaling.Should().BeNull();
        summaries.Single(s => s.Workload.BatchSize == 100).Scaling.Should().BeNull();
    }

    [Fact]
    public void MatrixOnlyVariesBatchSizeForWriteScenariosAndSupportsMixedOverrides()
    {
        var options = Options.Parse(["--profile", "smoke", "--scenarios", "stream,mixed", "--readers", "3", "--writers", "2"]);
        var workloads = options.Workloads().Distinct().ToArray();
        workloads.Where(w => w.Scenario == "stream").Should().HaveCount(2);
        workloads.Where(w => w.Scenario == "mixed").Should().HaveCount(2);
        workloads.Where(w => w.Scenario == "mixed").Should().OnlyContain(w => w.Readers == 3 && w.Writers == 2);
        Action parseInvalidOptions = () => Options.Parse(["--operations", "0"]);
        parseInvalidOptions.Should().ThrowExactly<ArgumentException>();
    }
}
