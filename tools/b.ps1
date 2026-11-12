Set-Location 'G:\#kHz Analyzer'
Get-Process kHzAnalyzer -EA SilentlyContinue | Stop-Process -Force
dotnet build KhzAnalyzer\KhzAnalyzer.csproj -c Debug -p:Platform=x64 -v q -nologo 2>&1 |
  Select-String -Pattern 'error|Erro\(s\)|Aviso\(s\)' | Select-Object -Unique -First 40 |
  ForEach-Object { ($_.Line.Replace('G:\#kHz Analyzer\KhzAnalyzer\','') -replace '\s\[G:.*\]','') }
