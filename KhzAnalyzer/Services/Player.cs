using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KhzAnalyzer.Models;
using KhzAnalyzer.Utils;
using Microsoft.UI.Dispatching;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace KhzAnalyzer.Services;

/// <summary>
/// The app's one audio player (singleton), so Space plays and pauses from any page.
/// The Analyzer page sets <see cref="QueueProvider"/> to the visible rows for next / previous.
/// Events are raised on the UI thread.
/// </summary>
public sealed class Player
{
    public static Player Instance { get; } = new();

    private readonly MediaPlayer _mp = new() { AudioCategory = MediaPlayerAudioCategory.Media };
    private PcmFile? _file;
    private DispatcherQueue? _ui;

    public TrackRow? Current { get; private set; }
    public TimeSpan Duration { get; private set; }
    public TimeSpan Position => _mp.PlaybackSession.Position;
    public bool IsPlaying => _mp.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
    public double Volume { get => _mp.Volume; set => _mp.Volume = Math.Clamp(value, 0, 1); }
    public bool Repeat { get => _mp.IsLoopingEnabled; set => _mp.IsLoopingEnabled = value; }
    public Func<IReadOnlyList<TrackRow>>? QueueProvider { get; set; }

    public event Action? TrackChanged;
    public event Action? StateChanged;
    public event Action<string>? Failed;

    private Player()
    {
        _mp.PlaybackSession.PlaybackStateChanged += (_, _) => OnUi(() => StateChanged?.Invoke());
        _mp.MediaEnded += (_, _) => OnUi(() => { if (!Repeat) Next(); });
        _mp.MediaFailed += (_, e) => { Logger.Log($"Media failed: {e.Error} {e.ErrorMessage} {e.ExtendedErrorCode?.Message}"); OnUi(() => Failed?.Invoke(e.ErrorMessage)); };
        _mp.PlaybackSession.PlaybackStateChanged += (s, _) => Logger.Log($"Playback state: {s.PlaybackState}");
    }

    private void OnUi(Action a)
    {
        if (_ui is null || _ui.HasThreadAccess) a(); else _ui.TryEnqueue(() => a());
    }

    public async Task PlayAsync(TrackRow row)
    {
        _ui ??= DispatcherQueue.GetForCurrentThread();
        Logger.Log($"Play: {row.CaminhoCompleto}");
        if (row.Status is StatusFaixa.Erro or StatusFaixa.Offline)
        {
            Failed?.Invoke(row.Status == StatusFaixa.Offline ? "This file is not downloaded to this computer." : "This file cannot be read.");
            return;
        }
        try
        {
            var file = await Task.Run(() => new PcmFile(row.CaminhoCompleto));
            _mp.Pause();
            _mp.Source = null;
            _file?.Dispose();
            _file = file;
            Current = row;
            Duration = file.Duration;
            _mp.Source = MediaSource.CreateFromMediaStreamSource(file.CreateSource());
            _mp.Play();
            TrackChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Log($"Playback failed for {row.CaminhoCompleto}: {ex}");
            Failed?.Invoke($"Could not play this file: {ex.Message}");
        }
    }

    public void Toggle()
    {
        Logger.Log($"Player toggle (current: {Current?.Faixa ?? "none"}, playing: {IsPlaying})");
        if (Current is null)
        {
            var q = QueueProvider?.Invoke();
            if (q is { Count: > 0 }) _ = PlayAsync(q[0]);
            return;
        }
        if (IsPlaying) _mp.Pause(); else _mp.Play();
    }

    public void Seek(TimeSpan to)
    {
        if (Current is null) return;
        if (to < TimeSpan.Zero) to = TimeSpan.Zero;
        if (to > Duration) to = Duration;
        _mp.PlaybackSession.Position = to;
    }

    public void SeekBy(double seconds) => Seek(Position + TimeSpan.FromSeconds(seconds));

    public void Next() => Step(+1);
    public void Previous()
    {
        if (Position.TotalSeconds > 3) { Seek(TimeSpan.Zero); return; }
        Step(-1);
    }

    private void Step(int dir)
    {
        var q = QueueProvider?.Invoke();
        if (q is null || q.Count == 0) return;
        int i = Current is null ? -1 : IndexOf(q, Current);
        for (int n = 0; n < q.Count; n++)
        {
            i = (i + dir + q.Count) % q.Count;
            if (q[i].Status is not (StatusFaixa.Erro or StatusFaixa.Offline)) { _ = PlayAsync(q[i]); return; }
        }
    }

    private static int IndexOf(IReadOnlyList<TrackRow> q, TrackRow row)
    {
        for (int i = 0; i < q.Count; i++) if (ReferenceEquals(q[i], row) || q[i].CaminhoCompleto == row.CaminhoCompleto) return i;
        return -1;
    }

    /// <summary>Stops if the given row is playing (used when a row is removed from the list).</summary>
    public void StopIf(TrackRow row)
    {
        if (Current is null || Current.CaminhoCompleto != row.CaminhoCompleto) return;
        _mp.Pause(); _mp.Source = null; _file?.Dispose(); _file = null; Current = null; Duration = TimeSpan.Zero;
        TrackChanged?.Invoke();
    }
}
