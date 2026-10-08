using System.Collections.Concurrent;

namespace NepalHub.Api.Features.Email;

/// <summary>
/// The quiet period per address between two sends, so one mailbox cannot be turned into a
/// flood by a loop that found a working endpoint: the first message goes out, and until the
/// configured window has passed, later ones are silently dropped while the endpoint keeps
/// answering exactly as it would for the first — because a different answer for a second
/// call would tell a caller which addresses exist.
///
/// <para>
/// The table lives in memory, which matches the deployment: one host, one process, one
/// address space. A future multi-instance deployment would move this beside the data it
/// protects; nothing else in this class would change.
/// </para>
/// </summary>
public sealed class EmailSendCooldown(EmailSettings settings, TimeProvider timeProvider)
{
    /// <summary>
    /// Prunes rather than grows without bound: an address can appear at most once, but a
    /// sweep that accepts arbitrary addresses could collect many, so finished entries are
    /// dropped whenever the table has grown past a size no human-scale traffic reaches.
    /// </summary>
    private const int PruneThreshold = 512;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSent =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Records a send for <paramref name="address"/> and reports whether one may go out now:
    /// true the first time and after the window has passed, false while it is still open.
    /// </summary>
    public bool TryEnter(string address)
    {
        var key = address.Trim();
        if (key.Length == 0)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        var window = settings.SendCooldown;

        if (_lastSent.Count > PruneThreshold)
        {
            Prune(now, window);
        }

        while (true)
        {
            if (_lastSent.TryGetValue(key, out var last))
            {
                if (now - last < window)
                {
                    return false;
                }

                if (_lastSent.TryUpdate(key, now, last))
                {
                    return true;
                }

                // Another thread moved the entry on; look again rather than guess.
                continue;
            }

            if (_lastSent.TryAdd(key, now))
            {
                return true;
            }

            // A competing add won between the lookup and here; look again.
        }
    }

    private void Prune(DateTimeOffset now, TimeSpan window)
    {
        foreach (var entry in _lastSent)
        {
            if (now - entry.Value >= window)
            {
                _lastSent.TryRemove(new KeyValuePair<string, DateTimeOffset>(entry.Key, entry.Value));
            }
        }
    }
}
