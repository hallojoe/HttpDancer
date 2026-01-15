using System.Diagnostics;

namespace Gr8Io.Threading.Tasks;

public static class DelayUntilExtensions
{
    /// <summary>
    /// Delays until the specified offset from the start stopwatch timestamp has elapsed.
    /// Uses coarse waiting for long delays and finer waiting as the target time approaches.
    /// </summary>
    public static async Task DelayUntilAsync(
        long startStopwatchTimestamp,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = Stopwatch.GetElapsedTime(startStopwatchTimestamp);
            var remaining = offset - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            var sleep =
                remaining > TimeSpan.FromHours(1) ? TimeSpan.FromMinutes(15) :
                remaining > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(1) :
                remaining;

            await Task.Delay(sleep, cancellationToken).ConfigureAwait(false);
        }
    }

}