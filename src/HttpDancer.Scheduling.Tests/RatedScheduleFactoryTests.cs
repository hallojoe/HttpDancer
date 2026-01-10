using HttpDancer.Scheduling.RatedScheduling;

namespace HttpDancer.Scheduling.Tests;

public class RatedScheduleFactoryTests
{
    [Test]
    public void Create_UsesRateToDistributeOffsets_WhenNoMinDuration()
    {
        var options = new RatedScheduleOptions
        {
            Capacity = 4,
            Rate = 2,
            RateWindow = TimeSpan.FromSeconds(1)
        };

        var sut = new RatedScheduleFactory();

        var schedule = sut.Create(options);

        TestContext.WriteLine("Generated offsets for 4 actions at 2/sec: {0}",
            string.Join(", ", schedule.Offsets.Select(ts => ts.TotalMilliseconds + "ms")));

        Assert.That(schedule.Capacity, Is.EqualTo(4));
        Assert.That(schedule.Duration, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(schedule.Offsets, Is.EqualTo(new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(1500)
        }));
    }

    [Test]
    public void Create_StretchesOffsetsToMinDuration_WhenProvided()
    {
        var options = new RatedScheduleOptions
        {
            Capacity = 2,
            Rate = 1,
            RateWindow = TimeSpan.FromSeconds(1),
            MinDuration = TimeSpan.FromSeconds(10)
        };

        var sut = new RatedScheduleFactory();

        var schedule = sut.Create(options);

        TestContext.WriteLine("Stretched schedule to MinDuration=10s with offsets: {0}",
            string.Join(", ", schedule.Offsets.Select(ts => ts.TotalSeconds + "s")));

        Assert.That(schedule.Duration, Is.EqualTo(TimeSpan.FromSeconds(10)));
        Assert.That(schedule.Offsets, Is.EqualTo(new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5)
        }));
    }

    [Test]
    public void Create_ReturnsEmptyWhenCapacityIsZeroOrLess()
    {
        var options = new RatedScheduleOptions
        {
            Capacity = 0
        };

        var sut = new RatedScheduleFactory();

        var schedule = sut.Create(options);

        TestContext.WriteLine("Capacity <= 0 produced empty schedule with duration {0}", schedule.Duration);

        Assert.That(schedule.Offsets, Is.Empty);
        Assert.That(schedule.Duration, Is.EqualTo(TimeSpan.Zero));
        Assert.That(schedule.Capacity, Is.EqualTo(0));
    }
}
