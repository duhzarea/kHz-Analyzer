# Builds the release files of kHz Analyzer into .\dist
#   dist\kHzAnalyzer\                    the app (unpackaged, self-contained, x64)
#   dist\kHzAnalyzer-<version>-portable.zip   extract anywhere and run kHzAnalyzer.exe
#   dist\kHzAnalyzer-<version>-setup.exe      installer with a destination-folder page (needs Inno Setup 6)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$versao = ([xml](Get-Content "$raiz\KhzAnalyzer\KhzAnalyzer.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$saida = "$raiz\dist\kHzAnalyzer"

if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }
dotnet publish "$raiz\KhzAnalyzer\KhzAnalyzer.csproj" -c Release -r win-x64 -p:Platform=x64 -o $saida --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Get-ChildItem $saida -Filter *.pdb -Recurse | Remove-Item -Force

$zip = "$raiz\dist\kHzAnalyzer-$versao-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $saida -DestinationPath $zip -CompressionLevel Optimal
"Portable zip: $zip ({0:N1} MB)" -f ((Get-Item $zip).Length / 1MB)

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) {
    & $iscc /Q "/DAppVersion=$versao" "$raiz\installer\kHzAnalyzer.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
    $setup = "$raiz\dist\kHzAnalyzer-$versao-setup.exe"
    "Setup program: $setup ({0:N1} MB)" -f ((Get-Item $setup).Length / 1MB)
} else {
    "Inno Setup 6 not found: setup program skipped (winget install JRSoftware.InnoSetup)."
}
