using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace KhzAnalyzer.Core;

public static class SpectrumAnalyzer
{
    public const double FreqAlvo = 19000;
    public const double MargemDb = 3.0;

    public static double PisoDeRuidoTeorico(int bits)
    {
        if (bits <= 0) bits = 16;
        return -(6.02 * bits + 1.76);
    }

    // Variante A (Atual): completo · janela 65536 · média
    public static AnalysisResult AnalisarA(string caminho)
    {
        return AnalisarCompleto(caminho, 65536, false);
    }

    // Variante B: completo · janela 8192 · média
    public static AnalysisResult AnalisarB(string caminho)
    {
        return AnalisarCompleto(caminho, 8192, false);
    }

    // Variante C: amostrado · janela 8192 · média
    public static AnalysisResult AnalisarC(string caminho)
    {
        return AnalisarAmostrado(caminho, 8192, false, 40, false);
    }

    // Variante D: amostrado · janela 8192 · máximo
    public static AnalysisResult AnalisarD(string caminho)
    {
        return AnalisarAmostrado(caminho, 8192, true, 40, false);
    }

    // Variante F: amostrado · janela 65536 · média (Candidato a Motor Final)
    public static AnalysisResult AnalisarF(string caminho)
    {
        return AnalisarAmostrado(caminho, 65536, false, 80, false);
    }
    
    // Variante F2: igual a F para testar determinismo inter-processo
    public static AnalysisResult AnalisarF2(string caminho)
    {
        return AnalisarAmostrado(caminho, 65536, false, 80, false);
    }

    // Variante E: amostrado · janela 8192 · máximo · piso MEDIDO
    public static AnalysisResult AnalisarE(string caminho)
    {
        return AnalisarAmostrado(caminho, 8192, true, 40, true);
    }

    // Motor de Triagem original (Desativado no caminho de produção)
    // Mantido apenas para experimentação e histórico (vide docs/limite-tamanho-janela.md)
    public static AnalysisResult AnalisarTriagem(string caminho)
    {
        var res1 = AnalisarF(caminho);
        if (res1.Erro != null) return res1;

        if (res1.FreqMaximaDetectada < 18000.0)
        {
            return res1;
        }

        return AnalisarA(caminho);
    }

    // Retorna a melhor variante como default (A-Streaming)
    public static AnalysisResult Analisar(string caminho, bool usarTriagem = false)
    {
        if (usarTriagem) return AnalisarTriagem(caminho);
        return AnalisarA(caminho);
    }

    private static AnalysisResult AnalisarCompleto(string caminho, int windowSize, bool useMax)
    {
        AudioMetadata meta;
        try
        {
            meta = WavAiffReader.GetMetadata(caminho);
        }
        catch (Exception e)
        {
            return new AnalysisResult { Arquivo = caminho, Erro = $"não foi possível ler: {e.Message}" };
        }

        if (meta.TotalFrames < 1024)
            return new AnalysisResult { Arquivo = caminho, Erro = "arquivo muito curto" };

        double nyquist = meta.SampleRate / 2.0;
        if (nyquist < FreqAlvo)
        {
            return new AnalysisResult
            {
                Arquivo = caminho,
                SampleRate = meta.SampleRate,
                Bits = meta.Bits,
                FreqMaximaDetectada = nyquist,
                Veredito = $"ABAIXO (sample rate só permite até {nyquist:F0}Hz)",
            };
        }

        int segLen = 1;
        while (segLen * 2 <= windowSize && segLen * 2 <= meta.TotalFrames)
            segLen *= 2;
        if (segLen < 2) segLen = (int)Math.Min(2, meta.TotalFrames);

        int step = Math.Max(1, segLen / 2); // 50% de overlap
        int numSegs = meta.TotalFrames >= segLen ? (int)((meta.TotalFrames - segLen) / step) + 1 : 0;

        int bins = segLen / 2 + 1;
        var psdAccum = new double[bins];
        var psdOut = new double[bins];
        var buffer = new System.Numerics.Complex[segLen];
        int used = 0;

        double[] window = Fft.HannWindow(segLen);
        var freqs = Fft.Rfftfreq(segLen, 1.0 / meta.SampleRate);

        for (int s = 0; s < numSegs; s++)
        {
            long start = (long)s * step;
            if (start + segLen > meta.TotalFrames) break;

            float[] block = WavAiffReader.ReadBlock(meta, start, segLen);
            if (block.Length < segLen) continue;

            Fft.Periodogram(block, meta.SampleRate, window, buffer, psdOut);
            for (int k = 0; k < bins; k++)
            {
                psdAccum[k] += psdOut[k];
            }
            used++;
        }

        if (used == 0) used = 1;
        var psdAvg = new double[bins];
        for (int i = 0; i < bins; i++) psdAvg[i] = psdAccum[i] / used;

        double maxLog = double.NegativeInfinity;
        var psdDb = new double[bins];
        for (int i = 0; i < bins; i++)
        {
            psdDb[i] = 10 * Math.Log10(psdAvg[i] + 1e-20);
            if (psdDb[i] > maxLog) maxLog = psdDb[i];
        }

        double limiar = PisoDeRuidoTeorico(meta.Bits) + MargemDb;

        double freqMax = 0;
        bool achouNaBandaAlta = false;
        for (int i = 0; i < bins; i++)
        {
            if (freqs[i] >= 18000 && (psdDb[i] - maxLog) > limiar)
            {
                achouNaBandaAlta = true;
                if (freqs[i] > freqMax) freqMax = freqs[i];
            }
        }

        if (!achouNaBandaAlta)
        {
            for (int i = 0; i < bins; i++)
            {
                if (freqs[i] < 18000 && (psdDb[i] - maxLog) > limiar && freqs[i] > freqMax)
                    freqMax = freqs[i];
            }
        }

        string veredito = freqMax >= FreqAlvo ? "OK" : $"ABAIXO (só até ~{freqMax:F0}Hz)";

        return new AnalysisResult
        {
            Arquivo = caminho,
            SampleRate = meta.SampleRate,
            Bits = meta.Bits,
            FreqMaximaDetectada = Math.Round(freqMax, 1),
            Veredito = veredito,
        };
    }

    private static int GetStableHashCode(string str)
    {
        unchecked
        {
            int hash1 = 5381;
            int hash2 = hash1;
            for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0') break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }
            return hash1 + (hash2 * 1566083941);
        }
    }

    public static AnalysisResult AnalisarAmostrado(string caminho, int windowSize, bool useMax, int numJanelas, bool medirPiso)
    {
        AudioMetadata meta;
        try
        {
            meta = WavAiffReader.GetMetadata(caminho);
        }
        catch (Exception e)
        {
            return new AnalysisResult { Arquivo = caminho, Erro = $"não foi possível ler: {e.Message}" };
        }

        if (meta.TotalFrames < windowSize * 2)
            return AnalisarCompleto(caminho, windowSize, useMax); // fallback se for mto curto

        double nyquist = meta.SampleRate / 2.0;
        if (nyquist < FreqAlvo)
        {
            return new AnalysisResult
            {
                Arquivo = caminho,
                SampleRate = meta.SampleRate,
                Bits = meta.Bits,
                FreqMaximaDetectada = nyquist,
                Veredito = $"ABAIXO (sample rate só permite até {nyquist:F0}Hz)",
            };
        }

        // Pseudo-aleatório determinístico ancorado no arquivo
        int seed = GetStableHashCode(caminho) ^ meta.DataChunkLength.GetHashCode();
        var rng = new Random(seed);

        int freqsCount = windowSize / 2 + 1;
        var psdConsolidado = new double[freqsCount];
        var psdOut = new double[freqsCount];
        var buffer = new System.Numerics.Complex[windowSize];
        
        if (!useMax)
        {
            for (int i = 0; i < freqsCount; i++) psdConsolidado[i] = 0.0;
        }
        else
        {
            for (int i = 0; i < freqsCount; i++) psdConsolidado[i] = double.NegativeInfinity;
        }

        double[] window = Fft.HannWindow(windowSize);
        int validWindows = 0;
        long maxFrame = meta.TotalFrames - windowSize;

        // Distribui numJanelas tentando pegar posições
        // O loop varre 100 janelas no máximo para tentar encontrar 'numJanelas' janelas não-silenciosas
        for (int i = 0; i < numJanelas * 2.5 && validWindows < numJanelas; i++)
        {
            // Posição base uniforme + jitter
            double pct = (double)validWindows / numJanelas;
            long basePos = (long)(pct * maxFrame);
            long jitter = (long)((rng.NextDouble() - 0.5) * (maxFrame / numJanelas));
            long pos = Math.Clamp(basePos + jitter, 0, maxFrame);

            float[] block = WavAiffReader.ReadBlock(meta, pos, windowSize);
            if (block.Length < windowSize) continue;

            // Calcula RMS para pular silêncio
            double sumSq = 0;
            for (int j = 0; j < windowSize; j++) sumSq += block[j] * block[j];
            double rms = Math.Sqrt(sumSq / windowSize);
            
            // Limiar simples de silêncio: -60dB (0.001) para descartar pontas silenciosas
            if (rms < 0.001 && validWindows > 0) 
                continue; 

            // Aplica janela e FFT
            Fft.Periodogram(block, meta.SampleRate, window, buffer, psdOut);

            for (int k = 0; k < freqsCount; k++)
            {
                if (useMax)
                    psdConsolidado[k] = Math.Max(psdConsolidado[k], psdOut[k]);
                else
                    psdConsolidado[k] += psdOut[k]; // soma para média depois
            }
            validWindows++;
        }

        if (validWindows == 0)
            return AnalisarCompleto(caminho, windowSize, useMax);

        if (!useMax)
        {
            for (int k = 0; k < freqsCount; k++) psdConsolidado[k] /= validWindows;
        }

        double[] freqs = Fft.Rfftfreq(windowSize, 1.0 / meta.SampleRate);

        double maxLog = double.NegativeInfinity;
        var psdDb = new double[freqsCount];
        for (int i = 0; i < freqsCount; i++)
        {
            // Periodogram returns absolute squared magnitude
            psdDb[i] = 10 * Math.Log10(psdConsolidado[i] + 1e-20);
            if (psdDb[i] > maxLog) maxLog = psdDb[i];
        }

        double limiarDb;
        if (medirPiso)
        {
            // Mede na banda Nyquist - 1kHz a Nyquist (ex: 21.05k a 22.05k)
            double startPiso = nyquist - 1000;
            double somaPiso = 0;
            int countPiso = 0;
            for (int i = 0; i < freqsCount; i++)
            {
                if (freqs[i] >= startPiso && freqs[i] <= nyquist)
                {
                    somaPiso += psdDb[i];
                    countPiso++;
                }
            }
            double ruidoMedio = countPiso > 0 ? (somaPiso / countPiso) : (PisoDeRuidoTeorico(meta.Bits) + maxLog);
            // ruidoMedio já está em dB absoluto (não relativo ao maxLog). 
            // O limiar será o ruído + 3dB absoluto.
            limiarDb = ruidoMedio + MargemDb;
        }
        else
        {
            limiarDb = maxLog + PisoDeRuidoTeorico(meta.Bits) + MargemDb; // absoluto
        }

        double freqMax = 0;
        bool achouNaBandaAlta = false;
        for (int i = 0; i < freqsCount; i++)
        {
            if (freqs[i] >= 18000 && psdDb[i] > limiarDb)
            {
                achouNaBandaAlta = true;
                if (freqs[i] > freqMax) freqMax = freqs[i];
            }
        }

        if (!achouNaBandaAlta)
        {
            for (int i = 0; i < freqsCount; i++)
            {
                if (freqs[i] < 18000 && psdDb[i] > limiarDb && freqs[i] > freqMax)
                    freqMax = freqs[i];
            }
        }

        string veredito = freqMax >= FreqAlvo ? "OK" : $"ABAIXO (só até ~{freqMax:F0}Hz)";

        return new AnalysisResult
        {
            Arquivo = caminho,
            SampleRate = meta.SampleRate,
            Bits = meta.Bits,
            FreqMaximaDetectada = Math.Round(freqMax, 1),
            Veredito = veredito,
        };
    }
}
