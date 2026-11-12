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

    // Derived from the measured frequency through Verdict (20 kHz rule), never from the stored text,
    // so results cached under the old 19 kHz rule are judged by the current rule without re-analysis.
    public VerdictKind Kind => Verdict.Of(this);
    public bool EhOk => Kind is VerdictKind.Pass or VerdictKind.Mp3Range;
    public bool EhAbaixo => Kind is VerdictKind.Check;
}
