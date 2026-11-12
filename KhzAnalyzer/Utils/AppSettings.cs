using System;
using System.IO;
using System.Text.Json;

namespace KhzAnalyzer.Utils;

/// <summary>
/// User settings, stored as JSON in %LOCALAPPDATA%\kHzAnalyzer\settings.json.
/// The app is unpackaged, so it keeps its data out of the install folder:
/// the install folder can then live on any drive, including a read-only one.
/// </summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "Light";

    /// <summary>"On" (default), "System" (follow Windows Animation effects) or "Off".</summary>
    public string Motion { get; set; } = "On";

    /// <summary>Download &amp; Trim: last folder used, and last format (index in the format list).</summary>
    public string DownloadFolder { get; set; } = "";
    public int DownloadFormat { get; set; }

    /// <summary>Snippets: saved folder list and last options.</summary>
    public System.Collections.Generic.List<string> SnippetFolders { get; set; } = new();
    public int SnippetStart { get; set; } = 90;
    public int SnippetDuration { get; set; } = 120;
    public int SnippetBitrate { get; set; } = 192;
    public bool SnippetRecursive { get; set; }

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "kHzAnalyzer", "settings.json");

    public static AppSettings Current { get; } = Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Logger.Log($"Settings could not be read, using defaults: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logger.Log($"Settings could not be saved: {ex.Message}");
        }
    }
}
