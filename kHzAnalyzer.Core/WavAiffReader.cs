using System;
using System.IO;
using System.Text;

namespace KhzAnalyzer.Core;

public class AudioMetadata
{
    public string Path { get; set; } = "";
    public string Format { get; set; } = ""; // "WAV" ou "AIFF"
    public int SampleRate { get; set; }
    public int Bits { get; set; }
    public int Channels { get; set; }
    public long DataChunkOffset { get; set; }
    public long DataChunkLength { get; set; }
    public bool IsFloat { get; set; }
    
    public int BytesPerSample => Math.Max(Bits / 8, 1);
    public int FrameSize => BytesPerSample * Math.Max(Channels, 1);
    public long TotalFrames => FrameSize > 0 ? DataChunkLength / FrameSize : 0;
}

/// <summary>
/// Lê arquivos .wav (RIFF/WAVE) e .aiff/.aif (FORM/AIFF, incluindo AIFC comprimido em PCM)
/// sem depender de bibliotecas externas. Mixa todos os canais para mono, igual à versão Python.
/// </summary>
public static class WavAiffReader
{
    public static AudioData Read(string path)
    {
        var meta = GetMetadata(path);
        float[] samples = ReadBlock(meta, 0, (int)meta.TotalFrames);
        return new AudioData { Samples = samples, SampleRate = meta.SampleRate, Bits = meta.Bits };
    }

    public static AudioMetadata GetMetadata(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        string tag = Encoding.ASCII.GetString(reader.ReadBytes(4));
        return tag switch
        {
            "RIFF" => GetMetadataWav(path, reader),
            "FORM" => GetMetadataAiff(path, reader),
            _ => throw new InvalidDataException("formato não reconhecido (esperado RIFF/WAV ou FORM/AIFF)"),
        };
    }

    public static float[] ReadBlock(AudioMetadata meta, long startSample, int length)
    {
        if (startSample >= meta.TotalFrames || length <= 0)
            return Array.Empty<float>();

        long startByteOffset = startSample * meta.FrameSize;
        long maxBytes = meta.DataChunkLength - startByteOffset;
        if (maxBytes <= 0) return Array.Empty<float>();

        long bytesToRead = (long)length * meta.FrameSize;
        bytesToRead = Math.Min(bytesToRead, maxBytes);

        using var stream = File.OpenRead(meta.Path);
        stream.Seek(meta.DataChunkOffset + startByteOffset, SeekOrigin.Begin);
        
        using var reader = new BinaryReader(stream);
        byte[] dataBytes = reader.ReadBytes((int)bytesToRead);

        if (meta.Format == "WAV")
            return DecodePcmLittleEndian(dataBytes, meta.Bits, meta.Channels, meta.IsFloat);
        else
            return DecodePcmBigEndian(dataBytes, meta.Bits, meta.Channels);
    }

    // ---------------------------------------------------------------- WAV --

    private static AudioMetadata GetMetadataWav(string path, BinaryReader reader)
    {
        reader.ReadUInt32(); // tamanho do RIFF, não usado
        string wave = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (wave != "WAVE")
            throw new InvalidDataException("RIFF sem sub-chunk WAVE");

        int channels = 1, sampleRate = 0, bits = 16, audioFormat = 1;
        long dataChunkOffset = -1;
        long dataChunkLength = 0;
        bool hasFmt = false;
        bool hasData = false;

        long len = reader.BaseStream.Length;
        while (reader.BaseStream.Position + 8 <= len)
        {
            string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            uint chunkSize = reader.ReadUInt32();
            long chunkStart = reader.BaseStream.Position;
            long safeSize = Math.Min(chunkSize, (uint)Math.Max(0, len - chunkStart));

            if (chunkId == "fmt ")
            {
                audioFormat = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32();   // byte rate
                reader.ReadInt16();   // block align
                bits = reader.ReadInt16();
                hasFmt = true;
            }
            else if (chunkId == "data")
            {
                dataChunkOffset = chunkStart;
                dataChunkLength = safeSize;
                hasData = true;
            }

            if (hasFmt && hasData) break; // temos tudo que importa

            long next = chunkStart + chunkSize + (chunkSize % 2); // chunks alinhados em 2 bytes
            if (next <= chunkStart) break; // guarda contra chunk corrompido/loop infinito
            reader.BaseStream.Position = Math.Min(next, len);
        }

        if (!hasData)
            throw new InvalidDataException("chunk 'data' não encontrado");
        if (sampleRate <= 0)
            throw new InvalidDataException("chunk 'fmt ' não encontrado ou inválido");

        return new AudioMetadata
        {
            Path = path,
            Format = "WAV",
            SampleRate = sampleRate,
            Bits = bits,
            Channels = channels,
            DataChunkOffset = dataChunkOffset,
            DataChunkLength = dataChunkLength,
            IsFloat = audioFormat == 3
        };
    }

    private static float[] DecodePcmLittleEndian(byte[] data, int bits, int channels, bool isFloat)
    {
        int bytesPerSample = Math.Max(bits / 8, 1);
        int ch = Math.Max(channels, 1);
        int frameSize = bytesPerSample * ch;
        int frames = frameSize > 0 ? data.Length / frameSize : 0;
        var mono = new float[frames];

        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            int baseOffset = f * frameSize;
            for (int c = 0; c < ch; c++)
            {
                int offset = baseOffset + c * bytesPerSample;
                double val = isFloat && bits == 32
                    ? BitConverter.ToSingle(data, offset)
                    : isFloat && bits == 64
                        ? BitConverter.ToDouble(data, offset)
                        : bits switch
                        {
                            8 => (data[offset] - 128) / 128.0,
                            16 => BitConverter.ToInt16(data, offset) / 32768.0,
                            24 => Decode24LE(data, offset) / 8388608.0,
                            32 => BitConverter.ToInt32(data, offset) / 2147483648.0,
                            _ => 0.0,
                        };
                sum += val;
            }
            mono[f] = (float)(sum / ch);
        }
        return mono;
    }

    private static int Decode24LE(byte[] data, int offset)
    {
        int val = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000); // extensão de sinal
        return val;
    }

    // --------------------------------------------------------------- AIFF --

    private static AudioMetadata GetMetadataAiff(string path, BinaryReader reader)
    {
        reader.ReadUInt32BigEndianCompat(); // tamanho do FORM, não usado
        string formType = Encoding.ASCII.GetString(reader.ReadBytes(4)); // "AIFF" ou "AIFC"
        if (formType != "AIFF" && formType != "AIFC")
            throw new InvalidDataException("FORM sem sub-tipo AIFF/AIFC");

        int channels = 1, bits = 16;
        double sampleRate = 0;
        long dataChunkOffset = -1;
        long dataChunkLength = 0;
        bool hasComm = false;
        bool hasSsnd = false;

        long len = reader.BaseStream.Length;
        while (reader.BaseStream.Position + 8 <= len)
        {
            string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            uint chunkSize = ReadUInt32BE(reader);
            long chunkStart = reader.BaseStream.Position;
            long safeSize = Math.Min(chunkSize, (uint)Math.Max(0, len - chunkStart));

            if (chunkId == "COMM")
            {
                channels = ReadInt16BE(reader);
                reader.ReadBytes(4); // numSampleFrames, recalculado a partir do SSND
                bits = ReadInt16BE(reader);
                sampleRate = ReadExtended80(reader);
                hasComm = true;
            }
            else if (chunkId == "SSND")
            {
                uint offset = ReadUInt32BE(reader);
                reader.ReadBytes(4); // blockSize, não usado
                long dataLen = safeSize - 8 - offset;
                if (dataLen > 0)
                {
                    dataChunkOffset = reader.BaseStream.Position + offset;
                    dataChunkLength = dataLen;
                    hasSsnd = true;
                }
            }

            if (hasComm && hasSsnd) break;

            long next = chunkStart + chunkSize + (chunkSize % 2);
            if (next <= chunkStart) break;
            reader.BaseStream.Position = Math.Min(next, len);
        }

        if (!hasSsnd)
            throw new InvalidDataException("chunk 'SSND' não encontrado");
        if (sampleRate <= 0)
            throw new InvalidDataException("chunk 'COMM' não encontrado ou inválido");

        return new AudioMetadata
        {
            Path = path,
            Format = "AIFF",
            SampleRate = (int)Math.Round(sampleRate),
            Bits = bits,
            Channels = channels,
            DataChunkOffset = dataChunkOffset,
            DataChunkLength = dataChunkLength,
            IsFloat = false
        };
    }

    private static float[] DecodePcmBigEndian(byte[] data, int bits, int channels)
    {
        int bytesPerSample = Math.Max(bits / 8, 1);
        int ch = Math.Max(channels, 1);
        int frameSize = bytesPerSample * ch;
        int frames = frameSize > 0 ? data.Length / frameSize : 0;
        var mono = new float[frames];

        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            int baseOffset = f * frameSize;
            for (int c = 0; c < ch; c++)
            {
                int offset = baseOffset + c * bytesPerSample;
                double val = bits switch
                {
                    8 => (sbyte)data[offset] / 128.0,
                    16 => ReadInt16BE(data, offset) / 32768.0,
                    24 => Decode24BE(data, offset) / 8388608.0,
                    32 => ReadInt32BE(data, offset) / 2147483648.0,
                    _ => 0.0,
                };
                sum += val;
            }
            mono[f] = (float)(sum / ch);
        }
        return mono;
    }

    private static short ReadInt16BE(byte[] d, int o) => (short)((d[o] << 8) | d[o + 1]);

    private static int ReadInt32BE(byte[] d, int o) =>
        (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];

    private static int Decode24BE(byte[] d, int o)
    {
        int val = (d[o] << 16) | (d[o + 1] << 8) | d[o + 2];
        if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000);
        return val;
    }

    private static uint ReadUInt32BE(BinaryReader r)
    {
        byte[] b = r.ReadBytes(4);
        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }

    private static short ReadInt16BE(BinaryReader r)
    {
        byte[] b = r.ReadBytes(2);
        return (short)((b[0] << 8) | b[1]);
    }

    private static double ReadExtended80(BinaryReader r)
    {
        byte[] b = r.ReadBytes(10);
        int sign = (b[0] & 0x80) != 0 ? -1 : 1;
        int exponent = ((b[0] & 0x7F) << 8) | b[1];

        ulong mantissa = 0;
        for (int i = 2; i < 10; i++)
            mantissa = (mantissa << 8) | b[i];

        if (exponent == 0 && mantissa == 0) return 0;

        double value = mantissa * Math.Pow(2, exponent - 16383 - 63);
        return sign * value;
    }
}

internal static class BinaryReaderCompatExtensions
{
    public static uint ReadUInt32BigEndianCompat(this BinaryReader r)
    {
        byte[] b = r.ReadBytes(4);
        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
    }
}
