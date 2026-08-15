using System;
using System.IO;
using Xunit;
using KhzAnalyzer.Core;

namespace KhzAnalyzer.RegressionTests;

public class RegressionTest
{
    private readonly string _fixturesDir = @"..\..\..\..\fixtures";

    [Fact]
    public void Test_Sine_21kHz_24bit_44k()
    {
        var result = SpectrumAnalyzer.Analisar(Path.Combine(_fixturesDir, "sine_21khz_24b44k.wav"));
        
        Assert.Null(result.Erro);
        Assert.True(result.EhOk);
        Assert.Equal(44100, result.SampleRate);
        Assert.Equal(24, result.Bits);
        // Tolerance: +/- 150 Hz
        Assert.InRange(result.FreqMaximaDetectada, 21000 - 150, 21000 + 150);
    }

    [Fact]
    public void Test_Lowpass_8kHz_16bit_44k()
    {
        var result = SpectrumAnalyzer.Analisar(Path.Combine(_fixturesDir, "lowpass_8khz_16b44k.wav"));
        
        Assert.Null(result.Erro);
        Assert.False(result.EhOk);
        Assert.Equal(44100, result.SampleRate);
        Assert.Equal(16, result.Bits);
        Assert.InRange(result.FreqMaximaDetectada, 8000 - 150, 8000 + 150);
    }

    [Fact]
    public void Test_Nyquist_16kHz_16bit_32k()
    {
        var result = SpectrumAnalyzer.Analisar(Path.Combine(_fixturesDir, "nyquist_16khz_16b32k.wav"));
        
        Assert.Null(result.Erro);
        Assert.False(result.EhOk);
        Assert.Equal(32000, result.SampleRate);
        Assert.Equal(16, result.Bits);
        Assert.Equal(16000, result.FreqMaximaDetectada);
    }

    [Fact]
    public void Test_InvalidHeader()
    {
        var result = SpectrumAnalyzer.Analisar(Path.Combine(_fixturesDir, "invalid_header.wav"));
        
        Assert.NotNull(result.Erro);
    }

    [Fact]
    public void Test_Aiff_20500Hz_16bit_44k()
    {
        var result = SpectrumAnalyzer.Analisar(Path.Combine(_fixturesDir, "aiff_20500hz_16b44k.aiff"));
        
        Assert.Null(result.Erro);
        Assert.True(result.EhOk);
        Assert.Equal(44100, result.SampleRate);
        Assert.Equal(16, result.Bits);
        Assert.InRange(result.FreqMaximaDetectada, 20500 - 150, 20500 + 150);
    }

    [Fact]
    public void Test_Aiff_48k_24bit()
    {
        var result = SpectrumAnalyzer.Analisar(Path.Combine(_fixturesDir, "aiff_48k_24bit.aiff"));
        
        Assert.Null(result.Erro);
        Assert.True(result.EhOk);
        Assert.Equal(48000, result.SampleRate);
        Assert.Equal(24, result.Bits);
        Assert.InRange(result.FreqMaximaDetectada, 23000 - 150, 23000 + 150);
    }

    [Fact]
    public void Test_Stereo_21kHz()
    {
        var result = SpectrumAnalyzer.Analisar(Path.Combine(_fixturesDir, "stereo_21khz.wav"));
        
        Assert.Null(result.Erro);
        Assert.True(result.EhOk);
        Assert.Equal(44100, result.SampleRate);
        Assert.Equal(16, result.Bits);
        Assert.InRange(result.FreqMaximaDetectada, 21000 - 150, 21000 + 150);
    }

    [Fact]
    public void Test_Tiny_512Samples()
    {
        var result = SpectrumAnalyzer.Analisar(Path.Combine(_fixturesDir, "tiny_512samples.wav"));
        
        Assert.NotNull(result.Erro);
    }
}
