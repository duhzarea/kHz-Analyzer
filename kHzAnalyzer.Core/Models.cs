namespace KhzAnalyzer.Core;

/// <summary>Áudio decodificado: uma amostra mono (canais já mixados) + metadados do arquivo.</summary>
public sealed class AudioData
{
    public float[] Samples { get; init; } = System.Array.Empty<float>();
    public int SampleRate { get; init; }
    public int Bits { get; init; }
}

/// <summary>Resultado da análise de um único arquivo.</summary>
public sealed class AnalysisResult
{
    public string Arquivo { get; init; } = "";
    public int SampleRate { get; init; }
    public int Bits { get; init; }
    public double FreqMaximaDetectada { get; init; }
    public string Veredito { get; init; } = "";
    public string? Erro { get; init; }
    
    // Indica se a faixa não pôde ser analisada porque estava apenas na nuvem
    public bool IsOffline { get; init; }

    public bool EhOk => Erro is null && Veredito.StartsWith("OK");
    public bool EhAbaixo => Erro is null && Veredito.StartsWith("ABAIXO");
}
