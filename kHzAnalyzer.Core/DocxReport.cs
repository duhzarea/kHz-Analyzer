using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace KhzAnalyzer.Core;

public static class DocxReport
{
    public static void Generate(string path, IEnumerable<AnalysisResult> results)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        CreateEntry(archive, "[Content_Types].xml", ContentTypesXml());
        CreateEntry(archive, "_rels/.rels", RelsXml());
        CreateEntry(archive, "word/document.xml", DocumentXml(results));
    }

    private static void CreateEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }

    private static string ContentTypesXml() =>
        @"<?xml version=""1.0"" encoding=""utf-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
</Types>";

    private static string RelsXml() =>
        @"<?xml version=""1.0"" encoding=""utf-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
</Relationships>";

    private static string DocumentXml(IEnumerable<AnalysisResult> results)
    {
        var sb = new StringBuilder();
        sb.Append(@"<?xml version=""1.0"" encoding=""utf-8""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>");

        // Title
        sb.Append(@"<w:p><w:pPr><w:jc w:val=""center""/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val=""36""/></w:rPr><w:t>Spectral Analysis Report — 20 kHz threshold</w:t></w:r></w:p>");

        // Summary
        int total = results.Count();
        int ok = results.Count(r => r.Kind == VerdictKind.Pass);
        int mp3 = results.Count(r => r.Kind == VerdictKind.Mp3Range);
        int abaixo = results.Count(r => r.Kind == VerdictKind.Check);
        int offline = results.Count(r => r.Kind == VerdictKind.NotDownloaded);
        int erros = results.Count(r => r.Kind == VerdictKind.Unreadable);

        sb.Append($@"<w:p><w:pPr><w:jc w:val=""center""/></w:pPr><w:r><w:rPr><w:i/><w:color w:val=""555555""/><w:sz w:val=""20""/></w:rPr><w:t>{total} tracks  ·  {ok} reach 20 kHz+  ·  {Verdict.Attention} {abaixo} below 20 kHz  ·  {Verdict.Attention} {mp3} MP3 in the 19 kHz range  ·  {erros} unreadable  ·  {offline} not downloaded</w:t></w:r></w:p>");
        sb.Append(@"<w:p/>"); // empty line

        // Table
        sb.Append(@"<w:tbl>
  <w:tblPr>
    <w:tblW w:w=""0"" w:type=""auto""/>
    <w:tblBorders>
      <w:top w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""auto""/>
      <w:left w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""auto""/>
      <w:bottom w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""auto""/>
      <w:right w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""auto""/>
      <w:insideH w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""auto""/>
      <w:insideV w:val=""single"" w:sz=""4"" w:space=""0"" w:color=""auto""/>
    </w:tblBorders>
  </w:tblPr>");

        // Header Row
        sb.Append(@"<w:tr><w:trPr><w:tblHeader/></w:trPr>");
        string[] headers = { "TRACK", "KEY", "BPM", "TYPE", "SAMPLE RATE", "BITS", "MAX FREQ.", "VERDICT" };
        foreach (var h in headers)
        {
            sb.Append($@"<w:tc>
  <w:tcPr><w:shd w:val=""clear"" w:color=""auto"" w:fill=""1F1F1F""/><w:tcMargin w:top=""100"" w:left=""100"" w:bottom=""100"" w:right=""100""/></w:tcPr>
  <w:p><w:pPr><w:jc w:val=""left""/></w:pPr><w:r><w:rPr><w:b/><w:color w:val=""FFFFFF""/><w:sz w:val=""18""/></w:rPr><w:t>{h}</w:t></w:r></w:p>
</w:tc>");
        }
        sb.Append("</w:tr>");

        var ordered = results.OrderBy(r =>
        {
            var (_, _, faixa) = TrackNameParser.Parse(r.Arquivo);
            return faixa;
        });

        int rowIndex = 0;
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        foreach (var r in ordered)
        {
            var (key, bpm, faixa) = TrackNameParser.Parse(r.Arquivo);
            string formato = Path.GetExtension(r.Arquivo).TrimStart('.').ToUpperInvariant();
            
            string sr = r.Erro != null ? "-" : r.SampleRate.ToString("N0", culture) + " Hz";
            string bits = r.Erro != null ? "-" : r.Bits.ToString();
            string freq = r.Erro != null ? "-" : r.FreqMaximaDetectada.ToString("N0", culture) + " Hz";
            
            string vereditoTxt = Verdict.Label(r.Kind);
            
            string vColor = "000000";
            bool vBold = false;
            
            switch (r.Kind)
            {
                case VerdictKind.NotDownloaded: vColor = "64748B"; break;
                case VerdictKind.Unreadable: vColor = "B45309"; vBold = true; break;
                case VerdictKind.Check: vColor = "B91C1C"; vBold = true; break;
                case VerdictKind.Mp3Range: vColor = "B45309"; vBold = true; break;
                default: vColor = "166534"; break;
            }

            string fill = rowIndex % 2 == 1 ? "F5F5F5" : "FFFFFF";

            string[] cols = { faixa, key, bpm, formato, sr, bits, freq };
            
            sb.Append("<w:tr>");
            foreach (var col in cols)
            {
                string text = string.IsNullOrEmpty(col) ? "-" : SecurityElement.Escape(col);
                sb.Append($@"<w:tc>
  <w:tcPr><w:shd w:val=""clear"" w:color=""auto"" w:fill=""{fill}""/><w:tcMargin w:top=""100"" w:left=""100"" w:bottom=""100"" w:right=""100""/></w:tcPr>
  <w:p><w:pPr><w:jc w:val=""left""/></w:pPr><w:r><w:rPr><w:sz w:val=""18""/></w:rPr><w:t>{text}</w:t></w:r></w:p>
</w:tc>");
            }
            
            // Verdict col
            sb.Append($@"<w:tc>
  <w:tcPr><w:shd w:val=""clear"" w:color=""auto"" w:fill=""{fill}""/><w:tcMargin w:top=""100"" w:left=""100"" w:bottom=""100"" w:right=""100""/></w:tcPr>
  <w:p><w:pPr><w:jc w:val=""left""/></w:pPr><w:r><w:rPr><w:color w:val=""{vColor}""/>{(vBold ? "<w:b/>" : "")}<w:sz w:val=""18""/></w:rPr><w:t>{SecurityElement.Escape(vereditoTxt)}</w:t></w:r></w:p>
</w:tc>");

            sb.Append("</w:tr>");
            rowIndex++;
        }

        sb.Append("</w:tbl>");

        // A4 Landscape margins and size
        sb.Append(@"
    <w:sectPr>
      <w:pgSz w:w=""16838"" w:h=""11906"" w:orient=""landscape"" />
      <w:pgMar w:top=""1134"" w:right=""1134"" w:bottom=""1134"" w:left=""1134"" />
    </w:sectPr>
  </w:body>
</w:document>");

        return sb.ToString();
    }
}
