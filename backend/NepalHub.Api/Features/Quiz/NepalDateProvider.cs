namespace NepalHub.Api.Features.Quiz;

/// <summary>
/// Supplies the current Nepal-time date. A daily feature cannot ask the machine clock or
/// MySQL for "today": Nepal Standard Time is UTC+05:45, a 45-minute offset that no naive
/// "+06:00" shortcut gets right, and a visitor in Kathmandu at 00:30 must already be on
/// the new day's quiz even though UTC has not turned over yet.
/// </summary>
public interface INepalDateProvider
{
    /// <summary>The current date in Nepal Standard Time.</summary>
    DateOnly Today { get; }

    /// <summary>The date before <see cref="Today"/>, used for the streak comparison.</summary>
    DateOnly Yesterday { get; }

    /// <summary>The current instant, so timestamps are written in the same frame of reference.</summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// The current date and time in Nepal local time, for anything that compares a clock time
    /// against a Nepal schedule rather than a date against a deadline.
    /// </summary>
    DateTime NepalNow { get; }

    /// <summary>
    /// The UTC instant at which <paramref name="nepalDate"/> began in Nepal. Period windows are
    /// compared against stored UTC timestamps, so "today" has to become a boundary in the frame
    /// those timestamps were written in, and a "+06:00" shortcut is off by fifteen minutes for
    /// the whole window.
    /// </summary>
    DateTime UtcStartOfDay(DateOnly nepalDate);
}

public sealed class NepalDateProvider(TimeProvider timeProvider) : INepalDateProvider
{
    /// <summary>
    /// Nepal has had no daylight saving and has not moved its offset since 1986, so the
    /// offset is fixed. The zone is still resolved by id where the platform knows it,
    /// because that keeps the intent explicit; the literal offset is the fallback for a
    /// trimmed container image with no tzdata.
    /// </summary>
    private static readonly TimeSpan NepalOffset = TimeSpan.FromMinutes(5 * 60 + 45);

    private static readonly TimeZoneInfo? Zone = ResolveZone();

    public DateOnly Today => DateOnly.FromDateTime(Now());

    public DateOnly Yesterday => Today.AddDays(-1);

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateTime NepalNow => Now();

    public DateTime UtcStartOfDay(DateOnly nepalDate)
    {
        var localMidnight = nepalDate.ToDateTime(TimeOnly.MinValue);

        // Nepal has never observed daylight saving, so the fixed offset is a whole
        // answer rather than a fallback: midnight in Kathmandu is unambiguous.
        return Zone is null
            ? localMidnight - NepalOffset
            : TimeZoneInfo.ConvertTimeToUtc(localMidnight, Zone);
    }

    private DateTime Now()
    {
        var utc = timeProvider.GetUtcNow();

        return Zone is null
            ? utc.ToOffset(NepalOffset).DateTime
            : TimeZoneInfo.ConvertTime(utc, Zone).DateTime;
    }

    private static TimeZoneInfo? ResolveZone()
    {
        // Windows and Linux name the same zone differently, so both are tried before
        // falling back to the fixed offset.
        foreach (var id in (string[])["Asia/Kathmandu", "Nepal Standard Time"])
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next identifier.
            }
            catch (InvalidTimeZoneException)
            {
                // Corrupt zone data on this machine; the fixed offset still works.
            }
        }

        return null;
    }
}
