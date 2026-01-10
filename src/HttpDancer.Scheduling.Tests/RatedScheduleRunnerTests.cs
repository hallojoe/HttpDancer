using System.Diagnostics;
using HttpDancer.Scheduling.RatedScheduling;
using NSubstitute;

namespace HttpDancer.Scheduling.Tests;

public class RatedScheduleRunnerTests
{
    public interface ITestExecutor
    {
        Task ExecuteAsync(int index, TimeSpan offset, CancellationToken cancellationToken);
    }

    [Test]
    public async Task RunAsync_GroupsOffsetsByToleranceAndPreservesIndexOrder()
    {
        var executor = Substitute.For<ITestExecutor>();
        executor.ExecuteAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var settings = new RatedScheduleRunnerSettings
        {
            BatchTolerance = TimeSpan.FromMilliseconds(10),
            MaxDegreeOfParallelism = 1
        };

        var runner = new RatedScheduleRunner(settings);

        var schedule = new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(4),
            TimeSpan.FromMilliseconds(20)
        };

        var startTimestamp = StartTimestampForElapsed(TimeSpan.FromSeconds(5));

        await runner.RunAsync(
            schedule,
            (index, offset, token) => executor.ExecuteAsync(index, offset, token),
            startTimestamp,
            maxDegreeOfParallelism: 1,
            cancellationToken: CancellationToken.None);

        TestContext.WriteLine("Executed offsets at tolerance 10ms: {0}",
            string.Join(", ", schedule.Select(ts => ts.TotalMilliseconds + "ms")));

        Received.InOrder(() =>
        {
            executor.ExecuteAsync(0, TimeSpan.Zero, Arg.Any<CancellationToken>());
            executor.ExecuteAsync(1, TimeSpan.FromMilliseconds(4), Arg.Any<CancellationToken>());
            executor.ExecuteAsync(2, TimeSpan.FromMilliseconds(20), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task RunAsync_SkipsNegativeOffsetsWhenConfigured()
    {
        var executor = Substitute.For<ITestExecutor>();
        executor.ExecuteAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = new RatedScheduleRunner(new RatedScheduleRunnerSettings
        {
            SkipNegativeOffsets = true,
            MaxDegreeOfParallelism = 1
        });

        var schedule = new[]
        {
            TimeSpan.FromMilliseconds(-1),
            TimeSpan.Zero
        };

        var startTimestamp = StartTimestampForElapsed(TimeSpan.FromSeconds(1));

        await runner.RunAsync(
            schedule,
            (index, offset, token) => executor.ExecuteAsync(index, offset, token),
            startTimestamp,
            cancellationToken: CancellationToken.None);

        await executor.Received(1).ExecuteAsync(1, TimeSpan.Zero, Arg.Any<CancellationToken>());

        TestContext.WriteLine("Negative offset skipped; executed count: 1 (index 1).");
    }

    [Test]
    public void RunAsync_ThrowsOnNegativeOffsetWhenNotSkipping()
    {
        var executor = Substitute.For<ITestExecutor>();
        executor.ExecuteAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var runner = new RatedScheduleRunner(new RatedScheduleRunnerSettings
        {
            SkipNegativeOffsets = false
        });

        var schedule = new[]
        {
            TimeSpan.FromMilliseconds(-1),
            TimeSpan.Zero
        };

        var startTimestamp = StartTimestampForElapsed(TimeSpan.FromSeconds(1));

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            runner.RunAsync(
                schedule,
                (index, offset, token) => executor.ExecuteAsync(index, offset, token),
                startTimestamp,
                cancellationToken: CancellationToken.None));

        executor.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default, default);

        TestContext.WriteLine("Negative offset caused ArgumentOutOfRangeException as expected.");
    }

    [Test]
    public async Task RunAsync_RespectsMaxDegreeOfParallelism_ForLargeSchedule()
    {
        // Test avoids long wall-clock waits. It seeds startTimestamp in the past,
        // so every offset is immediately due, then uses only a short Task.Delay(20ms)
        // inside the executor to simulate work and measure concurrency.
        
        var options = new RatedScheduleOptions
        {
            Capacity = 320,
            Rate = 10,
            RateWindow = TimeSpan.FromSeconds(1)
        };

        var schedule = new RatedScheduleFactory().Create(options).Offsets;

        var runner = new RatedScheduleRunner(new RatedScheduleRunnerSettings
        {
            MaxDegreeOfParallelism = 5
        });

        var current = 0;
        var maxObserved = 0;
        var executed = 0;

        // Start far enough in the past so all offsets are immediately due.
        var startTimestamp = StartTimestampForElapsed(TimeSpan.FromSeconds(30));

        await runner.RunAsync(
            schedule,
            async (_, _, token) =>
            {
                var running = Interlocked.Increment(ref current);

                // Track the highest concurrent executions seen.
                int observed;
                do
                {
                    observed = maxObserved;
                    if (running <= observed)
                    {
                        break;
                    }
                }
                while (Interlocked.CompareExchange(ref maxObserved, running, observed) != observed);

                await Task.Delay(TimeSpan.FromMilliseconds(20), token);

                Interlocked.Increment(ref executed);
                Interlocked.Decrement(ref current);
            },
            startTimestamp,
            maxDegreeOfParallelism: 5,
            cancellationToken: CancellationToken.None);

        TestContext.WriteLine("Executed {0} items; max observed concurrency: {1}", executed, maxObserved);

        Assert.That(executed, Is.EqualTo(options.Capacity));
        Assert.That(maxObserved, Is.EqualTo(5));
    }

    private static long StartTimestampForElapsed(TimeSpan elapsed)
    {
        var stopwatchTicks = (long)(elapsed.TotalSeconds * Stopwatch.Frequency);
        return Stopwatch.GetTimestamp() - stopwatchTicks;
    }
}
