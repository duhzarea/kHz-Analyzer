using System.Collections.Generic;
using System.Threading;

namespace KhzAnalyzer.Utils;

/// <summary>
/// Every cancellable task in flight — batch analysis, downloads, snippet rendering.
/// Esc asks this, not a single page, so the key works from anywhere in the shell.
/// </summary>
public static class TaskRegistry
{
    private static readonly List<CancellationTokenSource> _live = new();

    /// <summary>Registers a token source and hands it back, so callers can wrap the assignment.</summary>
    public static CancellationTokenSource Track(CancellationTokenSource cts)
    {
        lock (_live) _live.Add(cts);
        return cts;
    }

    /// <summary>Unregisters and disposes. Safe to call with null or twice.</summary>
    public static void Release(CancellationTokenSource? cts)
    {
        if (cts is null) return;
        lock (_live) _live.Remove(cts);
        try { cts.Dispose(); } catch { }
    }

    /// <summary>Cancels everything running. True when at least one task was actually stopped.</summary>
    public static bool CancelAll()
    {
        CancellationTokenSource[] snapshot;
        lock (_live) snapshot = _live.ToArray();

        bool stopped = false;
        foreach (var cts in snapshot)
        {
            try
            {
                if (cts.IsCancellationRequested) continue;
                cts.Cancel();
                stopped = true;
            }
            catch { /* already disposed by its own finally */ }
        }
        return stopped;
    }
}
