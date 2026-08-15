using System;
using System.Numerics;

namespace KhzAnalyzer.Core;

/// <summary>FFT iterativa radix-2 (Cooley-Tukey) in-place. O tamanho do buffer deve ser potência de 2.</summary>
public static class Fft
{
    public static void Transform(Complex[] buffer)
    {
        int n = buffer.Length;
        if (n <= 1) return;

        // permutação por bit-reversal
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2 * Math.PI / len;
            var wlen = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += len)
            {
                Complex w = Complex.One;
                int half = len / 2;
                for (int k = 0; k < half; k++)
                {
                    Complex u = buffer[i + k];
                    Complex v = buffer[i + k + half] * w;
                    buffer[i + k] = u + v;
                    buffer[i + k + half] = u - v;
                    w *= wlen;
                }
            }
        }
    }

    /// <summary>
    /// Periodograma médio estilo scipy.signal.welch: divide o sinal em segmentos com 50% de
    /// sobreposição, aplica janela de Hann, faz FFT de cada um e tira a média das potências.
    /// A escala absoluta não é idêntica ao scipy, mas isso não importa aqui: o critério da
    /// aplicação só compara o espectro relativo ao próprio pico (dB relativo), e essa relação
    /// se preserva independente da constante de escala.
    /// </summary>
    public static (double[] freqs, double[] psd) Welch(float[] signal, int sampleRate, int nperseg)
    {
        int segLen = 1;
        while (segLen * 2 <= nperseg && segLen * 2 <= signal.Length)
            segLen *= 2;
        if (segLen < 2) segLen = Math.Min(2, signal.Length == 0 ? 2 : signal.Length);

        int step = Math.Max(1, segLen / 2); // 50% de overlap
        int numSegs = signal.Length >= segLen ? (signal.Length - segLen) / step + 1 : 0;

        double[] window = HannWindow(segLen);
        double windowPowerSum = 0;
        foreach (double w in window) windowPowerSum += w * w;
        if (windowPowerSum <= 0) windowPowerSum = 1;

        int bins = segLen / 2 + 1;
        var psdAccum = new double[bins];
        int used = 0;

        for (int s = 0; s < numSegs; s++)
        {
            int start = s * step;
            if (start + segLen > signal.Length) break;

            var buffer = new Complex[segLen];
            for (int i = 0; i < segLen; i++)
                buffer[i] = new Complex(signal[start + i] * window[i], 0);

            Transform(buffer);

            for (int k = 0; k < bins; k++)
            {
                double mag = buffer[k].Magnitude;
                double power = (mag * mag) / (sampleRate * windowPowerSum);
                if (k > 0 && k < bins - 1) power *= 2; // espectro de um lado só
                psdAccum[k] += power;
            }
            used++;
        }

        if (used == 0) used = 1;
        var psd = new double[bins];
        for (int i = 0; i < bins; i++) psd[i] = psdAccum[i] / used;

        var freqs = new double[bins];
        for (int i = 0; i < bins; i++) freqs[i] = i * (double)sampleRate / segLen;

        return (freqs, psd);
    }

    private static double[] HannWindow(int n)
    {
        var w = new double[n];
        if (n == 1) { w[0] = 1; return w; }
        for (int i = 0; i < n; i++)
            w[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1));
        return w;
    }
}
