using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using KhzAnalyzer.Core.Media;
using KhzAnalyzer.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KhzAnalyzer.Pages;

/// <summary>One line of the results list.</summary>
public sealed class SnippetRow
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string Message { get; init; } = "";
    public string Glyph { get; init; } = "";
    public Brush Brush { get; init; } = null!;
}

public sealed partial class SnippetsPage : Page
{
    private readonly ObservableCollection<string> _folders = new();
    private readonly ObservableCollection<SnippetRow> _rows = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    public SnippetsPage()
    {
        InitializeComponent();

        var s = AppSettings.Current;
        foreach (string f in s.SnippetFolders) _folders.Add(f);
        FolderList.ItemsSource = _folders;
        ResultList.ItemsSource = _rows;

        StartBox.Value = s.SnippetStart;
        DurationBox.Value = s.SnippetDuration;
        BitrateBox.SelectedIndex = s.SnippetBitrate switch { 128 => 0, 256 => 2, 320 => 3, _ => 1 };
        RecursiveBox.IsChecked = s.SnippetRecursive;

        ToolsMissingBar.IsOpen = MediaTools.Ffmpeg is null;
        UpdateButtons();
    }

    // ---------------------------------------------------------- Folders --

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
            var escolhida = await picker.PickSingleFolderAsync();
            if (escolhida is null || string.IsNullOrWhiteSpace(escolhida.Path)) return;

            if (_folders.Contains(escolhida.Path, StringComparer.OrdinalIgnoreCase))
            {
                StatusText.Text = "That folder is already in the list.";
            }
            else
            {
                _folders.Add(escolhida.Path);
                SaveFolders();
            }
            FolderList.SelectedItem = _folders.First(f => f.Equals(escolhida.Path, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not open the folder picker ({ex.Message}).";
        }
        UpdateButtons();
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is string pasta)
        {
            _folders.Remove(pasta);
            SaveFolders();
            StatusText.Text = "Removed from the list. Nothing was deleted from disk.";
        }
        UpdateButtons();
    }

    private void FolderList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void SaveFolders()
    {
        AppSettings.Current.SnippetFolders = _folders.ToList();
        AppSettings.Current.Save();
    }

    // -------------------------------------------------------------- Run --

    private void RunSelected_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is string pasta) Run(new[] { pasta });
    }

    private void RunAll_Click(object sender, RoutedEventArgs e) => Run(_folders.ToList());

    private async void Run(IReadOnlyList<string> pastas)
    {
        var ausentes = pastas.Where(p => !Directory.Exists(p)).ToList();
        var existentes = pastas.Where(Directory.Exists).ToList();
        if (existentes.Count == 0)
        {
            StatusText.Text = "The folder could not be found. Is its drive connected?";
            return;
        }

        var opcoes = new SnippetOptions
        {
            StartSeconds = double.IsNaN(StartBox.Value) ? 90 : (int)StartBox.Value,
            DurationSeconds = double.IsNaN(DurationBox.Value) ? 120 : (int)DurationBox.Value,
            BitrateKbps = int.Parse((string)((ComboBoxItem)BitrateBox.SelectedItem).Tag),
            Recursive = RecursiveBox.IsChecked == true,
            DryRun = DryRunBox.IsChecked == true,
        };

        var s = AppSettings.Current;
        s.SnippetStart = opcoes.StartSeconds;
        s.SnippetDuration = opcoes.DurationSeconds;
        s.SnippetBitrate = opcoes.BitrateKbps;
        s.SnippetRecursive = opcoes.Recursive;
        s.Save();

        _rows.Clear();
        _busy = true;
        UpdateButtons();
        Progress.IsIndeterminate = true;
        Progress.Value = 0;
        StatusText.Text = "Looking for tracks without a snippet…";
        _cts = TaskRegistry.Track(new CancellationTokenSource());

        var progresso = new Progress<SnippetProgress>(p =>
        {
            if (p.Total > 0)
            {
                Progress.IsIndeterminate = false;
                Progress.Maximum = p.Total;
                Progress.Value = p.Done;
            }
            if (p.Finished is { } ev)
                _rows.Insert(0, ToRow(ev));
            else if (p.CurrentFile.Length > 0)
                StatusText.Text = $"[{p.Done + 1}/{p.Total}] {p.CurrentFile}";
        });

        try
        {
            Logger.Log($"Snippets: {existentes.Count} folder(s), start {opcoes.StartSeconds}s, length {opcoes.DurationSeconds}s, {opcoes.BitrateKbps}k, recursive={opcoes.Recursive}, dry={opcoes.DryRun}");
            // Off the UI thread: the work must not block the window, and progress updates
            // then reach the window in order, before the final summary below.
            var token = _cts.Token;
            var r = await System.Threading.Tasks.Task.Run(() => SnippetGenerator.RunAsync(existentes, opcoes, progresso, token));

            string criados = r.DryRun ? $"{r.Created} would be created" : $"{r.Created} created";
            string falhas = r.Failed > 0 ? $" · {r.Failed} failed" : "";
            string faltam = ausentes.Count > 0 ? $" · {ausentes.Count} folder(s) not found" : "";
            StatusText.Text = r.Created == 0 && r.Failed == 0
                ? $"Nothing to do: all {r.AlreadyExisted} tracks already have a snippet.{faltam}"
                : $"Done in {r.Elapsed.TotalSeconds:F0} s — {criados} · {r.AlreadyExisted} already had one{falhas}{faltam}";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled. Snippets already created were kept.";
        }
        catch (Exception ex)
        {
            Logger.Log($"Snippets failed: {ex}");
            StatusText.Text = ex is MediaToolException ? ex.Message : $"Unexpected error: {ex.Message}";
        }
        finally
        {
            Progress.IsIndeterminate = false;
            _busy = false;
            TaskRegistry.Release(_cts);
            _cts = null;
            UpdateButtons();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelButton.IsEnabled = false;
        StatusText.Text = "Cancelling…";
    }

    // ---------------------------------------------------------- Helpers --

    private SnippetRow ToRow(SnippetEvent ev)
    {
        (string glyph, string brush) = ev.Outcome switch
        {
            SnippetOutcome.Created => ("", "SuccessBrush"),
            SnippetOutcome.WouldCreate => ("", "TextSecondaryBrush"),
            _ => ("", "DangerBrush"),
        };
        return new SnippetRow
        {
            Name = System.IO.Path.GetFileName(ev.SourcePath),
            Path = ev.Outcome == SnippetOutcome.Failed ? ev.SourcePath : ev.OutputPath,
            Message = ev.Message,
            Glyph = glyph,
            Brush = ThemeBrush(brush),
        };
    }

    /// <summary>Resolves a brush from Tokens.xaml for the theme the window is showing now.</summary>
    private Brush ThemeBrush(string key)
    {
        string tema = ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        foreach (var dic in Application.Current.Resources.MergedDictionaries)
        {
            if (dic.ThemeDictionaries.TryGetValue(tema, out var t) && t is ResourceDictionary rd && rd.TryGetValue(key, out var b) && b is Brush brush)
                return brush;
        }
        return (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
    }

    private void UpdateButtons()
    {
        bool ffmpeg = !ToolsMissingBar.IsOpen;
        NoFoldersText.Visibility = _folders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AddFolderButton.IsEnabled = !_busy;
        RemoveFolderButton.IsEnabled = !_busy && FolderList.SelectedItem is not null;
        RunSelectedButton.IsEnabled = !_busy && ffmpeg && FolderList.SelectedItem is not null;
        RunAllButton.IsEnabled = !_busy && ffmpeg && _folders.Count > 0;
        CancelButton.IsEnabled = _busy;
        OptionsPanel.IsHitTestVisible = !_busy;
        OptionsPanel.Opacity = _busy ? 0.6 : 1;
        FolderList.IsEnabled = !_busy;
    }
}
