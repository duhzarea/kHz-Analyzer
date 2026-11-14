using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KhzAnalyzer.Core;
using KhzAnalyzer.Models;
using KhzAnalyzer.Services;
using KhzAnalyzer.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace KhzAnalyzer.Pages;

public sealed partial class HomePage : Page, IShortcutTarget
{
    private static readonly string[] Extensoes = { ".wav", ".aiff", ".aif" };

    private readonly List<TrackRow> _todos = new();
    private readonly ObservableCollection<TrackRow> _visiveis = new();
    private readonly List<AnalysisResult> _resultadosRaw = new();
    private CancellationTokenSource? _cts;

    public HomePage()
    {
        InitializeComponent();
        ResultadosList.ItemsSource = _visiveis;
        // ListViewItem keeps Enter for itself; listen to handled events too.
        ResultadosList.AddHandler(KeyDownEvent, new KeyEventHandler(ResultadosList_KeyDown), true);
        Player.Instance.QueueProvider = () => _visiveis.ToList();
        Player.Instance.Failed += msg => StatusText.Text = msg;
        Player.Instance.TrackChanged += () =>
        {
            var cur = Player.Instance.Current;
            if (cur is not null && _visiveis.Contains(cur)) { ResultadosList.SelectedItem = cur; ResultadosList.ScrollIntoView(cur); }
        };
        bool primeira = true;
        Loaded += (_, _) =>
        {
            if (!primeira) return;
            primeira = false;
            Motion.Entrance(new UIElement[] { Head, Actions, CardTotal, CardPass, CardBelow, CardErrors, CardOffline, TableCard, RightColumn }, staggerMs: 60);
        };
    }

    // ------------------------------------------------------------ Pasta --

    private async void ProcurarButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(
                XamlRoot.ContentIslandEnvironment.AppWindowId);

            var resultado = await picker.PickSingleFolderAsync();
            if (resultado is not null && !string.IsNullOrWhiteSpace(resultado.Path))
                PastaTextBox.Text = resultado.Path;
        }
        catch (Exception ex)
        {
            // Se o seletor falhar por qualquer motivo, o campo de texto continua
            // sendo um caminho válido de entrada — o usuário pode colar o caminho.
            StatusText.Text = $"Could not open the folder picker ({ex.Message}). Paste the path into the field instead.";
        }
    }

    // ---------------------------------------------------------- Análise --

    private async void AnalisarButton_Click(object sender, RoutedEventArgs e)
    {
        string pasta = PastaTextBox.Text?.Trim().Trim('"') ?? "";

        if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(pasta))
        {
            StatusText.Text = "Choose a valid folder before analyzing.";
            return;
        }

        _todos.Clear();
        _visiveis.Clear();
        _resultadosRaw.Clear();
        AtualizarContadores();

        _cts = TaskRegistry.Track(new CancellationTokenSource());
        AnalisarButton.IsEnabled = false;
        ProcurarButton.IsEnabled = false;
        CancelarButton.IsEnabled = true;
        ExportarButton.IsEnabled = false;

        try
        {
            await ExecutarAnaliseAsync(pasta, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = $"Analysis cancelled. {_todos.Count} tracks already analyzed were kept.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            AnalisarButton.IsEnabled = true;
            ProcurarButton.IsEnabled = true;
            CancelarButton.IsEnabled = false;
            ExportarButton.IsEnabled = _resultadosRaw.Count > 0;
            TaskRegistry.Release(_cts);
            _cts = null;
        }
    }

    private void CancelarButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelarButton.IsEnabled = false;
        StatusText.Text = "Cancelling…";
    }

    private async Task ExecutarAnaliseAsync(string pasta, CancellationToken token)
    {
        StatusText.Text = "Looking for files…";
        Logger.Log($"--- NOVA ANÁLISE ---");
        Logger.Log($"Iniciando enumeração na pasta: {pasta}");

        var cronometroEnumeracao = Stopwatch.StartNew();
        var arquivos = new List<string>();
        var arquivosOffline = new List<string>();

        var opcoes = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Temporary
        };

        await Task.Run(() =>
        {
            try
            {
                foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.*", opcoes))
                {
                    token.ThrowIfCancellationRequested();
                    string ext = Path.GetExtension(arquivo).ToLowerInvariant();
                    if (Extensoes.Contains(ext))
                    {
                        bool isOffline = false;
                        try { isOffline = File.GetAttributes(arquivo).HasFlag(FileAttributes.Offline); }
                        catch { /* ignora erro de acesso temporário */ }

                        if (isOffline)
                        {
                            arquivosOffline.Add(arquivo);
                            continue;
                        }

                        arquivos.Add(arquivo);
                        if (arquivos.Count % 100 == 0)
                        {
                            DispatcherQueue.TryEnqueue(() => 
                                StatusText.Text = $"Looking for files… ({arquivos.Count} found)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro durante enumeração: {ex}");
                throw;
            }
        }, token);

        arquivos.Sort(StringComparer.OrdinalIgnoreCase);
        cronometroEnumeracao.Stop();
        Logger.Log($"Enumeração concluída em {cronometroEnumeracao.Elapsed}. Válidos: {arquivos.Count}, Offline: {arquivosOffline.Count}");

        int total = arquivos.Count + arquivosOffline.Count;
        if (total == 0)
        {
            StatusText.Text = "No .wav or .aiff files found in this folder.";
            return;
        }

        // Adiciona os arquivos offline diretamente na tabela
        foreach (var offline in arquivosOffline)
        {
            var erroResult = new AnalysisResult { Arquivo = offline, Erro = "Cloud-only file (not downloaded)", IsOffline = true };
            var erroLinha = TrackRow.FromResult(erroResult);
            _todos.Add(erroLinha);
            _resultadosRaw.Add(erroResult);
            if (PassaNoFiltro(erroLinha)) _visiveis.Add(erroLinha);
        }

        Progresso.Maximum = total;
        Progresso.Value = arquivosOffline.Count;
        AtualizarContadores();

        var cronometroAnalise = Stopwatch.StartNew();
        int processados = arquivosOffline.Count;

        // Paraleliza, mas limita agressivamente: metade dos núcleos lógicos, teto absoluto de 4.
        // Uma configuração avançada estará disponível na Fase 5.
        int workers = Math.Min(4, Math.Max(1, Environment.ProcessorCount / 2));
        var semaforo = new SemaphoreSlim(workers);
        var tarefas = new List<Task>();
        
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kHzAnalyzer");
        Directory.CreateDirectory(appData);
        using var cache = new AnalysisCache(Path.Combine(appData, "cache.db"));

        foreach (string caminho in arquivos)
        {
            token.ThrowIfCancellationRequested();
            await semaforo.WaitAsync(token);

            tarefas.Add(Task.Run(() =>
            {
                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                try
                {
                    token.ThrowIfCancellationRequested();
                    Logger.Log($"[INICIO] {caminho}");
                    
                    AnalysisResult? resultado = cache.TryGet(caminho);
                    if (resultado == null)
                    {
                        resultado = SpectrumAnalyzer.Analisar(caminho);
                        cache.Save(resultado);
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var linha = TrackRow.FromResult(resultado);
                        Logger.Log($"[FIM] {caminho} - Status: {linha.Status}");
                        
                        _todos.Add(linha);
                        _resultadosRaw.Add(resultado);
                        if (PassaNoFiltro(linha)) _visiveis.Add(linha);

                        int feitos = Interlocked.Increment(ref processados);
                        Progresso.Value = feitos;
                        AtualizarContadores();
                        AtualizarStatus(feitos, total, cronometroAnalise.Elapsed, linha.Faixa, arquivosOffline.Count);
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ERRO FATAL EM FAIXA] {caminho}: {ex}");
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var erroResult = new AnalysisResult { Arquivo = caminho, Erro = ex.Message };
                        var erroLinha = TrackRow.FromResult(erroResult);
                        _todos.Add(erroLinha);
                        _resultadosRaw.Add(erroResult);
                        if (PassaNoFiltro(erroLinha)) _visiveis.Add(erroLinha);
                        
                        int feitos = Interlocked.Increment(ref processados);
                        Progresso.Value = feitos;
                        AtualizarContadores();
                        AtualizarStatus(feitos, total, cronometroAnalise.Elapsed, erroLinha.Faixa, arquivosOffline.Count);
                    });
                }
                finally
                {
                    semaforo.Release();
                }
            }, token));
        }

        await Task.WhenAll(tarefas);

        cronometroAnalise.Stop();
        Logger.Log($"Análise concluída em {cronometroAnalise.Elapsed}. Total analisados: {total}");
        
        int analisadosReais = total - arquivosOffline.Count;
        string avisoOffline = arquivosOffline.Count > 0 ? $" ({arquivosOffline.Count} not downloaded)" : "";
        StatusText.Text = $"Done in {FormatarTempo(cronometroAnalise.Elapsed)} — {analisadosReais} tracks analyzed{avisoOffline}.";
    }

    private void AtualizarStatus(int feitos, int total, TimeSpan decorrido, string faixaAtual, int offlineCount)
    {
        string eta = "";
        int analisados = feitos - offlineCount;
        if (analisados > 0 && feitos < total)
        {
            double porFaixa = decorrido.TotalSeconds / analisados;
            var restante = TimeSpan.FromSeconds(porFaixa * (total - feitos));
            eta = $" · ~{FormatarTempo(restante)} left";
        }

        StatusText.Text = $"[{feitos}/{total}] {faixaAtual}{eta}";
    }

    private static string FormatarTempo(TimeSpan t) =>
        t.TotalMinutes >= 1
            ? $"{(int)t.TotalMinutes} min {t.Seconds} s"
            : $"{t.Seconds} s";

    private void AtualizarContadores()
    {
        Motion.CountTo(TotalText, _todos.Count);
        Motion.CountTo(OkText, _todos.Count(r => r.Status == StatusFaixa.Ok));
        Motion.CountTo(AbaixoText, _todos.Count(r => r.Status is StatusFaixa.Abaixo or StatusFaixa.Mp3));
        Motion.CountTo(ErroText, _todos.Count(r => r.Status == StatusFaixa.Erro));
        Motion.CountTo(OfflineText, _todos.Count(r => r.Status == StatusFaixa.Offline));
    }

    // ---------------------------------------------------- Filtros/busca --

    private void Filtro_Changed(object sender, RoutedEventArgs e) => AplicarFiltro();

    private void BuscaBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            AplicarFiltro();
    }

    private bool PassaNoFiltro(TrackRow linha)
    {
        // Os controles podem ainda não existir durante a construção da página.
        if (FiltroOk is null || FiltroProblemas is null) return true;

        bool statusOk =
            (FiltroOk.IsChecked == true && linha.Status == StatusFaixa.Ok) ||
            (FiltroProblemas.IsChecked == true && linha.PrecisaVerificar) ||
            (FiltroOk.IsChecked != true && FiltroProblemas.IsChecked != true);

        string busca = BuscaBox?.Text?.Trim() ?? "";
        bool buscaOk = busca.Length == 0 ||
            linha.Faixa.Contains(busca, StringComparison.OrdinalIgnoreCase);

        return statusOk && buscaOk;
    }

    private void AplicarFiltro()
    {
        _visiveis.Clear();
        foreach (var linha in _todos.Where(PassaNoFiltro))
            _visiveis.Add(linha);
    }

    private async void ExportarButton_Click(object sender, RoutedEventArgs e)
    {
        string pasta = PastaTextBox.Text?.Trim().Trim('"') ?? "";
        if (string.IsNullOrWhiteSpace(pasta) || _resultadosRaw.Count == 0)
        {
            StatusText.Text = "There are no results to export.";
            return;
        }

        var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(
            XamlRoot.ContentIslandEnvironment.AppWindowId);
        
        var pastaDestino = await picker.PickSingleFolderAsync();
        if (pastaDestino is null || string.IsNullOrWhiteSpace(pastaDestino.Path))
        {
            return; // Cancelou
        }

        ExportarButton.IsEnabled = false;
        StatusText.Text = "Generating reports…";

        try
        {
            // Cria cópia segura para processamento assíncrono
            var resultados = _resultadosRaw.ToList();
            
            string csvPath = Path.Combine(pastaDestino.Path, "relatorio_20khz.csv");
            string docxPath = Path.Combine(pastaDestino.Path, "Relatorio_20kHz.docx");

            await Task.Run(() =>
            {
                CsvReport.Generate(csvPath, resultados);
                DocxReport.Generate(docxPath, resultados);
            });

            StatusText.Text = "The .csv and .docx reports were saved to the chosen folder.";
            
            // Tenta abrir o documento gerado para o usuário
            try { Process.Start(new ProcessStartInfo(docxPath) { UseShellExecute = true }); } catch { }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export failed: {ex.Message}";
        }
        finally
        {
            ExportarButton.IsEnabled = true;
        }
    }

    // ------------------------------------------------- Rows: play, info, menu --

    private TrackRow? _menuRow;

    private TrackRow? Selecionada => ResultadosList.SelectedItem as TrackRow;

    private void ResultadosList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var row = (e.OriginalSource as FrameworkElement)?.DataContext as TrackRow ?? Selecionada;
        if (row is not null) _ = Player.Instance.PlayAsync(row);
    }

    private void ResultadosList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && Selecionada is { } row)
        {
            _ = Player.Instance.PlayAsync(row);
            e.Handled = true;
        }
    }

    private void ResultadosList_SelectionChanged(object sender, SelectionChangedEventArgs e) => MostrarInfo(Selecionada);

    private void MostrarInfo(TrackRow? row)
    {
        InfoEmpty.Visibility = row is null ? Visibility.Visible : Visibility.Collapsed;
        InfoGrid.Visibility = row is null ? Visibility.Collapsed : Visibility.Visible;
        if (row is null) return;
        InfoTrack.Text = row.Faixa;
        InfoFreq.Text = row.FreqMax;
        InfoRate.Text = row.SampleRate;
        InfoBits.Text = row.Bits;
        InfoFormat.Text = row.Formato;
        InfoBadge.Background = row.BadgeBackground;
        InfoBadge.BorderBrush = row.BadgeBorder;
        InfoVerdict.Foreground = row.BadgeForeground;
        InfoVerdict.Text = row.Veredito;
    }

    private void RowMenu_Opening(object sender, object e)
    {
        _menuRow = ((sender as MenuFlyout)?.Target as FrameworkElement)?.DataContext as TrackRow;
        if (_menuRow is not null) ResultadosList.SelectedItem = _menuRow;
    }

    private void MenuPlay_Click(object sender, RoutedEventArgs e) { if (_menuRow is not null) _ = Player.Instance.PlayAsync(_menuRow); }
    private async void MenuInfo_Click(object sender, RoutedEventArgs e) { if (_menuRow is not null) await TrackDialogs.ShowInfoAsync(XamlRoot, _menuRow); }
    private async void MenuAnalysis_Click(object sender, RoutedEventArgs e) { if (_menuRow is not null) await TrackDialogs.ShowAnalysisAsync(XamlRoot, _menuRow); }

    private void MenuExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (_menuRow is null) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_menuRow.CaminhoCompleto}\"") { UseShellExecute = true }); }
        catch (Exception ex) { StatusText.Text = $"Could not open Explorer: {ex.Message}"; }
    }

    private void MenuRemove_Click(object sender, RoutedEventArgs e) { if (_menuRow is not null) Remover(_menuRow); }

    private void Remover(TrackRow row)
    {
        int idx = _visiveis.IndexOf(row);
        Player.Instance.StopIf(row);
        _todos.Remove(row);
        _visiveis.Remove(row);
        _resultadosRaw.RemoveAll(r => r.Arquivo == row.CaminhoCompleto);
        AtualizarContadores();
        StatusText.Text = $"Removed from the list: {row.Faixa}. The file was not touched.";
        if (_visiveis.Count > 0) ResultadosList.SelectedIndex = Math.Clamp(idx, 0, _visiveis.Count - 1);
        else MostrarInfo(null);
    }

    // ------------------------------------------------------ Shortcuts --

    public bool HandleShortcut(string id)
    {
        switch (id)
        {
            case Shortcuts.Browse: if (ProcurarButton.IsEnabled) ProcurarButton_Click(this, new RoutedEventArgs()); return true;
            case Shortcuts.Analyze: if (AnalisarButton.IsEnabled) AnalisarButton_Click(this, new RoutedEventArgs()); return true;
            case Shortcuts.Cancel: if (CancelarButton.IsEnabled) { CancelarButton_Click(this, new RoutedEventArgs()); return true; } return false;
            case Shortcuts.Export: if (ExportarButton.IsEnabled) ExportarButton_Click(this, new RoutedEventArgs()); return true;
            case Shortcuts.Search: BuscaBox.Focus(FocusState.Keyboard); return true;
            case Shortcuts.TrackInfo:
                var row = Selecionada ?? Player.Instance.Current;
                if (row is not null) _ = TrackDialogs.ShowInfoAsync(XamlRoot, row);
                return row is not null;
            case Shortcuts.Remove:
                if (Selecionada is { } sel) { Remover(sel); return true; }
                return false;
        }
        return false;
    }
}
