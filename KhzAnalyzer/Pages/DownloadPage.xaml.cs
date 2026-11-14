using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using KhzAnalyzer.Core.Media;
using KhzAnalyzer.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KhzAnalyzer.Pages;

public sealed partial class DownloadPage : Page
{
    private CancellationTokenSource? _cts;
    private MediaInfo? _info;
    private string? _infoUrl;
    private string? _savedPath;

    public DownloadPage()
    {
        InitializeComponent();

        string pasta = AppSettings.Current.DownloadFolder;
        if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(pasta))
            pasta = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        FolderBox.Text = pasta;

        FormatChoice.SelectedIndex = Math.Clamp(AppSettings.Current.DownloadFormat, 0, 3);

        var faltando = new System.Collections.Generic.List<string>();
        if (MediaTools.YtDlp is null) faltando.Add("yt-dlp");
        if (MediaTools.Ffmpeg is null) faltando.Add("FFmpeg");
        if (faltando.Count > 0)
        {
            ToolsMissingBar.Message = $"{string.Join(" and ", faltando)} could not be found in the app's \"tools\" folder or in PATH. Reinstall kHz Analyzer to restore them.";
            ToolsMissingBar.IsOpen = true;
            FetchButton.IsEnabled = false;
            DownloadButton.IsEnabled = false;
        }
    }

    // ------------------------------------------------------------- Info --

    private void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && FetchButton.IsEnabled)
        {
            e.Handled = true;
            FetchButton_Click(sender, e);
        }
    }

    private async void FetchButton_Click(object sender, RoutedEventArgs e)
    {
        string url = UrlBox.Text.Trim();
        if (!EhLink(url))
        {
            LogText.Text = "Paste a complete link, starting with https://";
            return;
        }

        SetBusy(true);
        Progress.IsIndeterminate = true;
        LogText.Text = "Reading the link…";
        _cts = TaskRegistry.Track(new CancellationTokenSource());
        try
        {
            await CarregarInfoAsync(url, _cts.Token);
            LogText.Text = "Ready.";
        }
        catch (OperationCanceledException)
        {
            LogText.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            Logger.Log($"Download & Trim, info failed: {ex}");
            LogText.Text = ex.Message;
        }
        finally
        {
            Progress.IsIndeterminate = false;
            SetBusy(false);
            TaskRegistry.Release(_cts);
            _cts = null;
        }
    }

    private async System.Threading.Tasks.Task CarregarInfoAsync(string url, CancellationToken token)
    {
        _info = await MediaDownloader.FetchInfoAsync(url, token);
        _infoUrl = url;

        InfoTitle.Text = _info.Title;
        string duracao = _info.DurationSeconds is { } d ? MediaDownloader.FormatTime(TimeSpan.FromSeconds(d)) : "unknown length";
        InfoMeta.Text = string.IsNullOrEmpty(_info.Uploader) ? duracao : $"{_info.Uploader} · {duracao}";
        ThumbImage.Source = Uri.TryCreate(_info.ThumbnailUrl, UriKind.Absolute, out var thumb) ? new BitmapImage(thumb) : null;
        PreviewCard.Visibility = Visibility.Visible;

        // Prefilled and fully selected: typing replaces the title, doing nothing keeps it.
        FileNameBox.Text = MediaDownloader.SafeFileName(_info.Title);
        FileNameBox.Focus(FocusState.Programmatic);
        FileNameBox.SelectAll();
    }

    // --------------------------------------------------------- Download --

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        string url = UrlBox.Text.Trim();
        if (!EhLink(url))
        {
            LogText.Text = "Paste a complete link, starting with https://";
            return;
        }
        if (!MediaDownloader.TryParseTime(StartBox.Text, out var inicio) || !MediaDownloader.TryParseTime(EndBox.Text, out var fim))
        {
            LogText.Text = "Check the trim times. Use HH:MM:SS, MM:SS or seconds.";
            return;
        }
        if (inicio is { } a && fim is { } b && b <= a)
        {
            LogText.Text = "The end time must be after the start time.";
            return;
        }
        string pasta = FolderBox.Text.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(pasta) || !Path.IsPathFullyQualified(pasta))
        {
            LogText.Text = "Choose the folder to save to.";
            return;
        }

        var formato = Enum.Parse<ExportFormat>((string)((RadioButton)FormatChoice.SelectedItem).Tag);

        SetBusy(true);
        Progress.Value = 0;
        Progress.IsIndeterminate = true;
        PercentText.Text = "";
        _cts = TaskRegistry.Track(new CancellationTokenSource());
        try
        {
            // Download pressed without "Get info" first, or the link changed since.
            if (_info is null || _infoUrl != url)
            {
                LogText.Text = "Reading the link…";
                string nomeDigitado = FileNameBox.Text;
                await CarregarInfoAsync(url, _cts.Token);
                // A name typed before the title was known is the user's choice: keep it.
                if (!string.IsNullOrWhiteSpace(nomeDigitado)) FileNameBox.Text = nomeDigitado;
            }
            if (inicio is { } i && _info!.DurationSeconds is { } total && i.TotalSeconds >= total)
            {
                LogText.Text = "The start time is past the end of the source.";
                return;
            }

            string nome = string.IsNullOrWhiteSpace(FileNameBox.Text) ? _info!.Title : FileNameBox.Text;

            AppSettings.Current.DownloadFolder = pasta;
            AppSettings.Current.DownloadFormat = FormatChoice.SelectedIndex;
            AppSettings.Current.Save();

            var progresso = new Progress<DownloadProgress>(p =>
            {
                if (p.Percent is { } v)
                {
                    Progress.IsIndeterminate = false;
                    Progress.Value = v;
                    PercentText.Text = $"{v:F0} %";
                }
                else
                {
                    Progress.IsIndeterminate = true;
                }
                LogText.Text = p.Line;
            });

            Logger.Log($"Download & Trim: {formato}, trim {inicio}–{fim}");
            _savedPath = await MediaDownloader.DownloadAsync(new DownloadRequest
            {
                Url = url,
                Format = formato,
                DestinationFolder = pasta,
                FileName = nome,
                Start = inicio,
                End = fim,
                SourceDurationSeconds = _info!.DurationSeconds,
            }, progresso, _cts.Token);

            Progress.IsIndeterminate = false;
            Progress.Value = 100;
            PercentText.Text = "100 %";
            LogText.Text = $"Saved {Path.GetFileName(_savedPath)}";
            DoneFileText.Text = _savedPath;
            DoneOverlay.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            Progress.Value = 0;
            PercentText.Text = "";
            LogText.Text = "Cancelled. Nothing was saved.";
        }
        catch (Exception ex)
        {
            Logger.Log($"Download & Trim failed: {ex}");
            Progress.Value = 0;
            PercentText.Text = "";
            LogText.Text = ex is MediaToolException ? ex.Message : $"Unexpected error: {ex.Message}";
        }
        finally
        {
            Progress.IsIndeterminate = false;
            SetBusy(false);
            TaskRegistry.Release(_cts);
            _cts = null;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelButton.IsEnabled = false;
        LogText.Text = "Cancelling…";
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
            var escolhida = await picker.PickSingleFolderAsync();
            if (escolhida is not null && !string.IsNullOrWhiteSpace(escolhida.Path))
                FolderBox.Text = escolhida.Path;
        }
        catch (Exception ex)
        {
            LogText.Text = $"Could not open the folder picker ({ex.Message}). Type the path instead.";
        }
    }

    // ------------------------------------------------------- Completion --

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_savedPath is null || !File.Exists(_savedPath)) return;
        try { Process.Start("explorer.exe", $"/select,\"{_savedPath}\""); }
        catch (Exception ex) { Logger.Log($"Could not open Explorer: {ex.Message}"); }
    }

    private void DoneClose_Click(object sender, RoutedEventArgs e)
    {
        DoneOverlay.Visibility = Visibility.Collapsed;
    }

    // ----------------------------------------------------------- Helpers --

    private void SetBusy(bool ocupado)
    {
        bool ferramentas = !ToolsMissingBar.IsOpen;
        FetchButton.IsEnabled = !ocupado && ferramentas;
        DownloadButton.IsEnabled = !ocupado && ferramentas;
        CancelButton.IsEnabled = ocupado;
        UrlBox.IsEnabled = !ocupado;
        OptionsPanel.IsHitTestVisible = !ocupado;
        OptionsPanel.Opacity = ocupado ? 0.6 : 1;
    }

    private static bool EhLink(string texto) =>
        Uri.TryCreate(texto, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);
}
