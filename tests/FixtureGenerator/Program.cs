using System;
using System.IO;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        string outDir = @"..\fixtures";
        Directory.CreateDirectory(outDir);

        // 1. sine_21khz_24b44k.wav
        WriteWav(Path.Combine(outDir, "sine_21khz_24b44k.wav"), 44100, 24, 1, GenerateSine(21000, 44100, 24, 2));

        // 2. lowpass_8khz_16b44k.wav
        WriteWav(Path.Combine(outDir, "lowpass_8khz_16b44k.wav"), 44100, 16, 1, GenerateSine(8000, 44100, 16, 2));

        // 3. nyquist_16khz_16b32k.wav
        WriteWav(Path.Combine(outDir, "nyquist_16khz_16b32k.wav"), 32000, 16, 1, GenerateSine(15500, 32000, 16, 2));

        // 4. invalid_header.wav
        File.WriteAllBytes(Path.Combine(outDir, "invalid_header.wav"), new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00 }); // TRUNCATED

        // 5. aiff_20500hz_16b44k.aiff
        WriteAiff(Path.Combine(outDir, "aiff_20500hz_16b44k.aiff"), 44100, 16, 1, GenerateSine(20500, 44100, 16, 2));

        // 6. aiff_48k_24bit.aiff
        WriteAiff(Path.Combine(outDir, "aiff_48k_24bit.aiff"), 48000, 24, 1, GenerateSine(23000, 48000, 24, 2));

        // 7. stereo_21khz.wav (Left 1kHz, Right 21kHz)
        WriteWav(Path.Combine(outDir, "stereo_21khz.wav"), 44100, 16, 2, GenerateStereo(1000, 21000, 44100, 16, 2));

        // 8. tiny_512samples.wav
        WriteWav(Path.Combine(outDir, "tiny_512samples.wav"), 44100, 16, 1, GenerateSine(1000, 44100, 16, 512 / 44100.0)); // < 1024 samples

        Console.WriteLine("Fixtures generated successfully.");
        
        string expectedMd = @"# Expected Results

- `sine_21khz_24b44k.wav`: OK (44100Hz, 24-bit, Max Freq ~21000Hz)
- `lowpass_8khz_16b44k.wav`: ABAIXO (44100Hz, 16-bit, Max Freq ~8000Hz)
- `nyquist_16khz_16b32k.wav`: ABAIXO (32000Hz, 16-bit, Max Freq ~15500Hz)
- `invalid_header.wav`: ERRO (Invalid Format)
- `aiff_20500hz_16b44k.aiff`: OK (44100Hz, 16-bit, Max Freq ~20500Hz)
- `aiff_48k_24bit.aiff`: OK (48000Hz, 24-bit, Max Freq ~23000Hz)
- `stereo_21khz.wav`: OK (44100Hz, 16-bit, Channels 2, Max Freq ~21000Hz after mono mix)
- `tiny_512samples.wav`: ERRO (Not enough samples)";
        File.WriteAllText(Path.Combine(outDir, "expected.md"), expectedMd);
    }

    static byte[] GenerateSine(double freq, int sampleRate, int bitDepth, double durationSec)
    {
        int samples = (int)(sampleRate * durationSec);
        int bytesPerSample = bitDepth / 8;
        byte[] buffer = new byte[samples * bytesPerSample];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double val = Math.Sin(2 * Math.PI * freq * t);

            if (bitDepth == 16)
            {
                short s = (short)(val * 32767);
                byte[] b = BitConverter.GetBytes(s);
                buffer[i * 2] = b[0];
                buffer[i * 2 + 1] = b[1];
            }
            else if (bitDepth == 24)
            {
                int s = (int)(val * 8388607);
                byte[] b = BitConverter.GetBytes(s);
                buffer[i * 3] = b[0];
                buffer[i * 3 + 1] = b[1];
                buffer[i * 3 + 2] = b[2];
            }
        }
        return buffer;
    }

    static byte[] GenerateStereo(double freqL, double freqR, int sampleRate, int bitDepth, double durationSec)
    {
        int samples = (int)(sampleRate * durationSec);
        int bytesPerSample = bitDepth / 8;
        byte[] buffer = new byte[samples * 2 * bytesPerSample];

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double valL = Math.Sin(2 * Math.PI * freqL * t);
            double valR = Math.Sin(2 * Math.PI * freqR * t);

            if (bitDepth == 16)
            {
                short sL = (short)(valL * 32767);
                short sR = (short)(valR * 32767);
                byte[] bL = BitConverter.GetBytes(sL);
                byte[] bR = BitConverter.GetBytes(sR);
                buffer[i * 4] = bL[0];
                buffer[i * 4 + 1] = bL[1];
                buffer[i * 4 + 2] = bR[0];
                buffer[i * 4 + 3] = bR[1];
            }
        }
        return buffer;
    }

    static void WriteWav(string path, int sampleRate, short bitDepth, short channels, byte[] audioData)
    {
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + audioData.Length);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * (bitDepth / 8));
        bw.Write((short)(channels * (bitDepth / 8)));
        bw.Write(bitDepth);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(audioData.Length);
        bw.Write(audioData);
    }

    static void WriteAiff(string path, int sampleRate, short bitDepth, short channels, byte[] audioDataLittleEndian)
    {
        // Convert audioData to Big Endian
        byte[] audioData = new byte[audioDataLittleEndian.Length];
        int bytesPerSample = bitDepth / 8;
        for (int i = 0; i < audioData.Length; i += bytesPerSample)
        {
            for (int j = 0; j < bytesPerSample; j++)
            {
                audioData[i + j] = audioDataLittleEndian[i + bytesPerSample - 1 - j];
            }
        }

        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        bw.Write(Encoding.ASCII.GetBytes("FORM"));
        int commChunkSize = 18;
        int ssndChunkSize = 8 + audioData.Length;
        bw.Write(ReverseBytes(4 + 8 + commChunkSize + 8 + ssndChunkSize));
        bw.Write(Encoding.ASCII.GetBytes("AIFF"));
        
        bw.Write(Encoding.ASCII.GetBytes("COMM"));
        bw.Write(ReverseBytes(commChunkSize));
        bw.Write(ReverseBytes(channels));
        bw.Write(ReverseBytes(audioData.Length / (channels * bytesPerSample)));
        bw.Write(ReverseBytes(bitDepth));
        bw.Write(GetIeee754Extended(sampleRate));

        bw.Write(Encoding.ASCII.GetBytes("SSND"));
        bw.Write(ReverseBytes(ssndChunkSize));
        bw.Write(ReverseBytes((uint)0)); // offset
        bw.Write(ReverseBytes((uint)0)); // blockSize
        bw.Write(audioData);
    }

    static int ReverseBytes(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    static short ReverseBytes(short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    static uint ReverseBytes(uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    static byte[] GetIeee754Extended(int sampleRate)
    {
        if (sampleRate == 44100) return new byte[] { 0x40, 0x0E, 0xAC, 0x44, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        if (sampleRate == 48000) return new byte[] { 0x40, 0x0E, 0xBB, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        if (sampleRate == 32000) return new byte[] { 0x40, 0x0D, 0xFA, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        throw new NotImplementedException();
    }
}
