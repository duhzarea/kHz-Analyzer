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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KhzAnalyzer.Pages;

public sealed partial class HomePage : Page
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
            StatusText.Text = $"Não foi possível abrir o seletor de pasta ({ex.Message}). Cole o caminho no campo ao lado.";
        }
    }

    // ---------------------------------------------------------- Análise --

    private async void AnalisarButton_Click(object sender, RoutedEventArgs e)
    {
        string pasta = PastaTextBox.Text?.Trim().Trim('"') ?? "";

        if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(pasta))
        {
            StatusText.Text = "Escolha uma pasta válida antes de analisar.";
            return;
        }

        _todos.Clear();
        _visiveis.Clear();
        _resultadosRaw.Clear();
        AtualizarContadores();

        _cts = new CancellationTokenSource();
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
            StatusText.Text = $"Análise cancelada. {_todos.Count} faixas já analisadas foram mantidas.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erro inesperado: {ex.Message}";
        }
        finally
        {
            AnalisarButton.IsEnabled = true;
            ProcurarButton.IsEnabled = true;
            CancelarButton.IsEnabled = false;
            ExportarButton.IsEnabled = _resultadosRaw.Count > 0;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelarButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelarButton.IsEnabled = false;
        StatusText.Text = "Cancelando…";
    }

    private async Task ExecutarAnaliseAsync(string pasta, CancellationToken token)
    {
        StatusText.Text = "Procurando arquivos…";

        List<string> arquivos = await Task.Run(() =>
            Directory.EnumerateFiles(pasta, "*.*", SearchOption.AllDirectories)
                .Where(f => Extensoes.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            token);

        int total = arquivos.Count;
        if (total == 0)
        {
            StatusText.Text = "Nenhum arquivo .wav ou .aiff encontrado nessa pasta.";
            return;
        }

        Progresso.Maximum = total;
        Progresso.Value = 0;

        var cronometro = Stopwatch.StartNew();
        int processados = 0;

        // Paraleliza usando os núcleos disponíveis, deixando um livre para a interface.
        int workers = Math.Max(1, Environment.ProcessorCount - 1);
        var semaforo = new SemaphoreSlim(workers);
        var tarefas = new List<Task>();

        foreach (string caminho in arquivos)
        {
            token.ThrowIfCancellationRequested();
            await semaforo.WaitAsync(token);

            tarefas.Add(Task.Run(() =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    AnalysisResult resultado = SpectrumAnalyzer.Analisar(caminho);
                    var linha = TrackRow.FromResult(resultado);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _todos.Add(linha);
                        _resultadosRaw.Add(resultado);
                        if (PassaNoFiltro(linha)) _visiveis.Add(linha);

                        int feitos = Interlocked.Increment(ref processados);
                        Progresso.Value = feitos;
                        AtualizarContadores();
                        AtualizarStatus(feitos, total, cronometro.Elapsed, linha.Faixa);
                    });
                }
                finally
                {
                    semaforo.Release();
                }
            }, token));
        }

        await Task.WhenAll(tarefas);

        cronometro.Stop();
        StatusText.Text = $"Concluído em {FormatarTempo(cronometro.Elapsed)} — {total} faixas analisadas.";
    }

    private void AtualizarStatus(int feitos, int total, TimeSpan decorrido, string faixaAtual)
    {
        string eta = "";
        if (feitos > 0 && feitos < total)
        {
            double porFaixa = decorrido.TotalSeconds / feitos;
            var restante = TimeSpan.FromSeconds(porFaixa * (total - feitos));
            eta = $" · restam ~{FormatarTempo(restante)}";
        }

        StatusText.Text = $"[{feitos}/{total}] {faixaAtual}{eta}";
    }

    private static string FormatarTempo(TimeSpan t) =>
        t.TotalMinutes >= 1
            ? $"{(int)t.TotalMinutes}min {t.Seconds}s"
            : $"{t.Seconds}s";

    private void AtualizarContadores()
    {
        TotalText.Text = _todos.Count.ToString();
        OkText.Text = _todos.Count(r => r.Status == StatusFaixa.Ok).ToString();
        AbaixoText.Text = _todos.Count(r => r.Status == StatusFaixa.Abaixo).ToString();
        ErroText.Text = _todos.Count(r => r.Status == StatusFaixa.Erro).ToString();
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
            (FiltroProblemas.IsChecked == true && linha.Status != StatusFaixa.Ok) ||
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
        if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(pasta) || _resultadosRaw.Count == 0)
        {
            StatusText.Text = "Não há resultados para exportar nesta pasta.";
            return;
        }

        ExportarButton.IsEnabled = false;
        StatusText.Text = "Gerando relatórios...";

        try
        {
            // Cria cópia segura para processamento assíncrono
            var resultados = _resultadosRaw.ToList();
            
            string csvPath = Path.Combine(pasta, "relatorio_20khz.csv");
            string docxPath = Path.Combine(pasta, "Relatorio_20kHz.docx");

            await Task.Run(() =>
            {
                CsvReport.Generate(csvPath, resultados);
                DocxReport.Generate(docxPath, resultados);
            });

            StatusText.Text = "Relatórios .csv e .docx gerados na pasta de análise.";
            
            // Tenta abrir o documento gerado para o usuário
            try { Process.Start(new ProcessStartInfo(docxPath) { UseShellExecute = true }); } catch { }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erro ao exportar: {ex.Message}";
        }
        finally
        {
            ExportarButton.IsEnabled = true;
        }
    }
}
