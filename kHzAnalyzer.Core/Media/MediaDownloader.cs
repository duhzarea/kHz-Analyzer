using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KhzAnalyzer.Core.Media;

public enum ExportFormat { Wav, M4a, Mp3, Mp4 }

public sealed record MediaInfo(string Title, string Uploader, double? DurationSeconds, string? ThumbnailUrl);

public sealed class DownloadRequest
{
    public required string Url { get; init; }
    public required ExportFormat Format { get; init; }
    public required string DestinationFolder { get; init; }
    /// <summary>File name without extension.</summary>
    public required string FileName { get; init; }
    public TimeSpan? Start { get; init; }
    public TimeSpan? End { get; init; }
    /// <summary>Length of the source, when known. Used only to compute progress.</summary>
    public double? SourceDurationSeconds { get; init; }
}

/// <param name="Percent">0–100, or null while a step that reports no progress is running.</param>
public sealed record DownloadProgress(double? Percent, string Stage, string Line);

public sealed class MediaToolException : Exception
{
    public MediaToolException(string message) : base(message) { }
}

/// <summary>Downloads audio or video with yt-dlp and trims/converts it with FFmpeg.</summary>
public static partial class MediaDownloader
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [GeneratedRegex(@"\[download\]\s+([\d.]+)%")]
    private static partial Regex PercentRegex();

    public static string Extension(ExportFormat f) => f switch
    {
        ExportFormat.Wav => ".wav",
        ExportFormat.M4a => ".m4a",
        ExportFormat.Mp3 => ".mp3",
        _ => ".mp4",
    };

    /// <summary>Accepts "SS", "MM:SS" or "HH:MM:SS", with optional decimals. Empty text means "not set".</summary>
    public static bool TryParseTime(string? text, out TimeSpan? value)
    {
        value = null;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return true;

        string[] partes = text.Split(':');
        if (partes.Length > 3) return false;

        double total = 0;
        for (int i = 0; i < partes.Length; i++)
        {
            bool ultimo = i == partes.Length - 1;
            if (!double.TryParse(partes[i], ultimo ? NumberStyles.AllowDecimalPoint : NumberStyles.None, Inv, out double n)) return false;
            if (partes.Length > 1 && i > 0 && n >= 60) return false;
            total = total * 60 + n;
        }
        value = TimeSpan.FromSeconds(total);
        return true;
    }

    public static string FormatTime(TimeSpan t) =>
        string.Create(Inv, $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}");

    /// <summary>Removes the characters Windows does not allow in a file name.</summary>
    public static string SafeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        char[] proibidos = Path.GetInvalidFileNameChars();
        foreach (char c in name)
            sb.Append(Array.IndexOf(proibidos, c) >= 0 ? ' ' : c);
        string limpo = Regex.Replace(sb.ToString(), @"\s+", " ").Trim().TrimEnd('.');
        if (limpo.Length > 150) limpo = limpo[..150].Trim();
        return limpo.Length == 0 ? "download" : limpo;
    }

    /// <summary>Reads title, thumbnail and duration. Downloads no media.</summary>
    public static async Task<MediaInfo> FetchInfoAsync(string url, CancellationToken token)
    {
        string ytdlp = MediaTools.YtDlp ?? throw new MediaToolException("yt-dlp was not found.");

        var saida = new StringBuilder();
        var erros = new List<string>();
        int codigo = await MediaTools.RunAsync(ytdlp,
            new[] { "--dump-single-json", "--no-playlist", "--no-warnings", "--encoding", "utf-8", url },
            l => saida.AppendLine(l), l => erros.Add(l), token);

        if (codigo != 0) throw new MediaToolException(Resumo(erros, "The link could not be read."));

        try
        {
            using var doc = JsonDocument.Parse(saida.ToString());
            var raiz = doc.RootElement;
            string titulo = Texto(raiz, "title") ?? "Untitled";
            string autor = Texto(raiz, "uploader") ?? Texto(raiz, "channel") ?? "";
            double? duracao = raiz.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : null;
            return new MediaInfo(titulo, autor, duracao, Texto(raiz, "thumbnail"));
        }
        catch (JsonException)
        {
            throw new MediaToolException("The link was read, but its description could not be understood.");
        }
    }

    /// <summary>Downloads, trims and converts. Returns the path of the file that was saved.</summary>
    public static async Task<string> DownloadAsync(DownloadRequest req, IProgress<DownloadProgress>? progress, CancellationToken token)
    {
        string ytdlp = MediaTools.YtDlp ?? throw new MediaToolException("yt-dlp was not found.");
        string ffmpeg = MediaTools.Ffmpeg ?? throw new MediaToolException("FFmpeg was not found.");

        if (req.Start is { } s && req.End is { } e && e <= s)
            throw new MediaToolException("The end time must be after the start time.");

        Directory.CreateDirectory(req.DestinationFolder);
        string destino = CaminhoLivre(req.DestinationFolder, SafeFileName(req.FileName), Extension(req.Format));

        string temp = Path.Combine(Path.GetTempPath(), "kHzAnalyzer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            string pronto = req.Format == ExportFormat.Mp4
                ? await BaixarVideoAsync(req, ytdlp, ffmpeg, temp, progress, token)
                : await BaixarAudioAsync(req, ytdlp, ffmpeg, temp, progress, token);

            if (!File.Exists(pronto) || new FileInfo(pronto).Length == 0)
                throw new MediaToolException("The export produced no file.");

            File.Move(pronto, destino);
            progress?.Report(new DownloadProgress(100, "Done", $"Saved {Path.GetFileName(destino)}"));
            return destino;
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<string> BaixarAudioAsync(DownloadRequest req, string ytdlp, string ffmpeg, string temp,
        IProgress<DownloadProgress>? progress, CancellationToken token)
    {
        // Step 1 (0–70 %): best audio stream, untouched.
        var erros = new List<string>();
        int codigo = await MediaTools.RunAsync(ytdlp,
            new[]
            {
                "--no-playlist", "--newline", "--no-warnings", "--encoding", "utf-8",
                "-f", "bestaudio/best", "--ffmpeg-location", ffmpeg,
                "-o", Path.Combine(temp, "source.%(ext)s"), req.Url,
            },
            l =>
            {
                var m = PercentRegex().Match(l);
                double? pct = m.Success ? double.Parse(m.Groups[1].Value, Inv) * 0.70 : null;
                if (m.Success || l.StartsWith('[')) progress?.Report(new DownloadProgress(pct, "Downloading", l.Trim()));
            },
            l => erros.Add(l), token);

        string? fonte = Directory.EnumerateFiles(temp, "source.*").FirstOrDefault(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase));
        if (codigo != 0 || fonte is null) throw new MediaToolException(Resumo(erros, "The download failed."));

        // Step 2 (70–100 %): trim and convert. -ss/-to after -i = sample-accurate cut.
        string saida = Path.Combine(temp, "out" + Extension(req.Format));
        var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-nostats", "-progress", "pipe:1", "-i", fonte };
        if (req.Start is { } ini) args.AddRange(new[] { "-ss", ini.TotalSeconds.ToString("F3", Inv) });
        if (req.End is { } fim) args.AddRange(new[] { "-to", fim.TotalSeconds.ToString("F3", Inv) });
        args.Add("-vn");
        args.AddRange(req.Format switch
        {
            ExportFormat.Wav => new[] { "-ar", "48000", "-c:a", "pcm_s24le" },
            ExportFormat.M4a => new[] { "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart" },
            _ => new[] { "-c:a", "libmp3lame", "-b:a", "320k" },
        });
        args.Add(saida);

        double inicio = req.Start?.TotalSeconds ?? 0;
        double? fimSeg = req.End?.TotalSeconds ?? req.SourceDurationSeconds;
        double? trecho = fimSeg is { } f2 && f2 > inicio ? f2 - inicio : null;

        erros.Clear();
        progress?.Report(new DownloadProgress(70, "Converting", "ffmpeg: converting…"));
        codigo = await MediaTools.RunAsync(ffmpeg, args,
            l =>
            {
                if (!l.StartsWith("out_time_us=", StringComparison.Ordinal)) return;
                if (!long.TryParse(l.AsSpan("out_time_us=".Length), NumberStyles.Integer, Inv, out long us) || us < 0) return;
                double seg = us / 1_000_000.0;
                double? pct = trecho is { } t ? 70 + Math.Min(1, seg / t) * 30 : null;
                progress?.Report(new DownloadProgress(pct, "Converting", string.Create(Inv, $"ffmpeg: {FormatTime(TimeSpan.FromSeconds(seg))} written")));
            },
            l => erros.Add(l), token);

        if (codigo != 0) throw new MediaToolException(Resumo(erros, "The conversion failed."));
        return saida;
    }

    private static async Task<string> BaixarVideoAsync(DownloadRequest req, string ytdlp, string ffmpeg, string temp,
        IProgress<DownloadProgress>? progress, CancellationToken token)
    {
        var args = new List<string>
        {
            "--no-playlist", "--newline", "--no-warnings", "--encoding", "utf-8",
            "-f", "bv*+ba/b", "--ffmpeg-location", ffmpeg,
        };
        if (req.Start is not null || req.End is not null)
        {
            string a = req.Start is { } s ? s.TotalSeconds.ToString("F3", Inv) : "0";
            string b = req.End is { } e ? e.TotalSeconds.ToString("F3", Inv) : "inf";
            args.AddRange(new[] { "--download-sections", $"*{a}-{b}", "--force-keyframes-at-cuts" });
        }
        args.AddRange(new[] { "--recode-video", "mp4", "-o", Path.Combine(temp, "video.%(ext)s"), req.Url });

        // Video and audio usually come as two downloads; each one counts for half of 0–90 %.
        int parte = 0;
        var erros = new List<string>();
        int codigo = await MediaTools.RunAsync(ytdlp, args,
            l =>
            {
                string linha = l.Trim();
                if (linha.StartsWith("[download] Destination:", StringComparison.Ordinal)) parte++;
                var m = PercentRegex().Match(linha);
                if (m.Success)
                {
                    double p = double.Parse(m.Groups[1].Value, Inv);
                    double pct = (Math.Clamp(parte - 1, 0, 1) + p / 100.0) / 2.0 * 90.0;
                    progress?.Report(new DownloadProgress(pct, "Downloading", linha));
                }
                else if (linha.StartsWith("[Merger]", StringComparison.Ordinal) || linha.StartsWith("[VideoConvertor]", StringComparison.Ordinal))
                {
                    progress?.Report(new DownloadProgress(null, "Converting", linha));
                }
                else if (linha.StartsWith('['))
                {
                    progress?.Report(new DownloadProgress(null, "Downloading", linha));
                }
            },
            l => erros.Add(l), token);

        string video = Path.Combine(temp, "video.mp4");
        if (codigo != 0 || !File.Exists(video)) throw new MediaToolException(Resumo(erros, "The video download failed."));
        return video;
    }

    private static string CaminhoLivre(string pasta, string nome, string ext)
    {
        string caminho = Path.Combine(pasta, nome + ext);
        for (int i = 2; File.Exists(caminho); i++)
            caminho = Path.Combine(pasta, $"{nome} ({i}){ext}");
        return caminho;
    }

    private static string? Texto(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Resumo(List<string> erros, string padrao)
    {
        string? erro = erros.LastOrDefault(l => l.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                       ?? erros.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (erro is null) return padrao;
        erro = erro.Replace("ERROR:", "", StringComparison.OrdinalIgnoreCase).Trim();
        return erro.Length > 300 ? erro[..300] + "…" : erro;
    }
}
