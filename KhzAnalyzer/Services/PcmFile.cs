using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using KhzAnalyzer.Core;
using Windows.Media.Core;
using Windows.Media.MediaProperties;

namespace KhzAnalyzer.Services;

/// <summary>
/// Plays WAV and AIFF straight from the file through a MediaStreamSource, decoded by the app itself
/// (Media Foundation has no AIFF decoder). Output is interleaved 32-bit float, mono duplicated to stereo.
/// The header is parsed by the same <see cref="WavAiffReader"/> the analysis engine uses.
/// </summary>
public sealed class PcmFile : IDisposable
{
    public AudioMetadata Meta { get; }
    public TimeSpan Duration { get; }
    private readonly FileStream _fs;
    private long _frame;
    private readonly object _lock = new();
    private const int ChunkFrames = 4096;

    public PcmFile(string path)
    {
        Meta = WavAiffReader.GetMetadata(path);
        if (Meta.SampleRate <= 0 || Meta.Channels <= 0) throw new InvalidDataException("Unsupported audio header.");
        Duration = TimeSpan.FromSeconds((double)Meta.TotalFrames / Meta.SampleRate);
        _fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
    }

    public MediaStreamSource CreateSource()
    {
        int outCh = Math.Min(2, Meta.Channels) == 1 ? 2 : 2;
        var props = AudioEncodingProperties.CreatePcm((uint)Meta.SampleRate, (uint)outCh, 32);
        props.Subtype = MediaEncodingSubtypes.Float;
        var desc = new AudioStreamDescriptor(props);
        var mss = new MediaStreamSource(desc) { CanSeek = true, Duration = Duration, BufferTime = TimeSpan.FromMilliseconds(250) };
        mss.Starting += (s, e) =>
        {
            var start = e.Request.StartPosition ?? TimeSpan.Zero;
            lock (_lock) _frame = Math.Clamp((long)(start.TotalSeconds * Meta.SampleRate), 0, Meta.TotalFrames);
            e.Request.SetActualStartPosition(TimeSpan.FromSeconds((double)_frame / Meta.SampleRate));
        };
        mss.SampleRequested += (s, e) =>
        {
            lock (_lock)
            {
                if (_frame >= Meta.TotalFrames) { e.Request.Sample = null; return; }
                int frames = (int)Math.Min(ChunkFrames, Meta.TotalFrames - _frame);
                var ts = TimeSpan.FromSeconds((double)_frame / Meta.SampleRate);
                byte[] pcm = ReadStereoFloat(_frame, frames);
                _frame += frames;
                var sample = MediaStreamSample.CreateFromBuffer(pcm.AsBuffer(), ts);
                sample.Duration = TimeSpan.FromSeconds((double)frames / Meta.SampleRate);
                e.Request.Sample = sample;
            }
        };
        return mss;
    }

    /// <summary>Reads <paramref name="frames"/> frames from <paramref name="start"/> as interleaved stereo float32 bytes.</summary>
    public byte[] ReadStereoFloat(long start, int frames)
    {
        var floats = ReadFrames(start, frames, out int got);
        var bytes = new byte[got * 2 * 4];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Interleaved stereo floats (L,R,…). Channels beyond two are dropped; mono is duplicated.</summary>
    public float[] ReadFrames(long start, int frames, out int got)
    {
        int ch = Meta.Channels, bps = Meta.BytesPerSample, fsz = Meta.FrameSize;
        long avail = Math.Max(0, Meta.TotalFrames - start);
        got = (int)Math.Min(frames, avail);
        var raw = new byte[got * fsz];
        _fs.Position = Meta.DataChunkOffset + start * fsz;
        int read = 0;
        while (read < raw.Length) { int n = _fs.Read(raw, read, raw.Length - read); if (n <= 0) break; read += n; }
        got = read / fsz;
        bool big = Meta.Format != "WAV";
        var outp = new float[got * 2];
        for (int f = 0; f < got; f++)
        {
            int baseIdx = f * fsz;
            float l = Sample(raw, baseIdx, bps, big);
            float r = ch > 1 ? Sample(raw, baseIdx + bps, bps, big) : l;
            outp[f * 2] = l; outp[f * 2 + 1] = r;
        }
        return outp;
    }

    private float Sample(byte[] b, int i, int bps, bool big)
    {
        if (Meta.IsFloat)
        {
            if (bps == 4) return BitConverter.ToSingle(b, i);
            if (bps == 8) return (float)BitConverter.ToDouble(b, i);
        }
        switch (bps)
        {
            case 1: return (b[i] - 128) / 128f; // 8-bit WAV is unsigned
            case 2:
                return (short)(big ? (b[i] << 8) | b[i + 1] : b[i] | (b[i + 1] << 8)) / 32768f;
            case 3:
                int v = big ? (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) : (b[i] << 8) | (b[i + 1] << 16) | (b[i + 2] << 24);
                return (v >> 8) / 8388608f;
            case 4:
                int w = big ? (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3] : BitConverter.ToInt32(b, i);
                return w / 2147483648f;
            default: return 0;
        }
    }

    /// <summary>Peak envelope in <paramref name="bars"/> buckets (0..1), sampled sparsely so it is fast on long files.</summary>
    public float[] Envelope(int bars)
    {
        var env = new float[bars];
        long total = Meta.TotalFrames;
        if (total <= 0) return env;
        const int probe = 2048, probes = 6;
        lock (_lock)
        {
            for (int i = 0; i < bars; i++)
            {
                long a = total * i / bars, b = total * (i + 1) / bars;
                float peak = 0;
                for (int p = 0; p < probes; p++)
                {
                    long at = a + (b - a) * p / probes;
                    var fr = ReadFrames(at, probe, out int got);
                    double sum = 0;
                    for (int k = 0; k < got * 2; k++) sum += fr[k] * fr[k];
                    float rms = got > 0 ? (float)Math.Sqrt(sum / (got * 2)) : 0;
                    if (rms > peak) peak = rms;
                }
                env[i] = peak;
            }
        }
        float max = 0; foreach (var v in env) max = Math.Max(max, v);
        if (max > 0) for (int i = 0; i < bars; i++) env[i] = (float)Math.Pow(env[i] / max, 0.8);
        return env;
    }

    public void Dispose() => _fs.Dispose();
}
