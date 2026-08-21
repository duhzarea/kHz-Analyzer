using System;
using System.IO;
using System.Linq;
using KhzAnalyzer.Core;

string targetDir = @"G:\#kHz Analyzer\tests\fixtures";
var files = Directory.EnumerateFiles(targetDir, "*.wav").Concat(Directory.EnumerateFiles(targetDir, "*.aiff")).ToList();
foreach (var f in files)
{
    var res = SpectrumAnalyzer.AnalisarA(f);
    Console.WriteLine($"{Path.GetFileName(f), -30} | {res.FreqMaximaDetectada, -8:F1} | {res.Veredito}");
}
