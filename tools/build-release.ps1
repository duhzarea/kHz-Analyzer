# Builds the release files of kHz Analyzer into .\dist
#   dist\kHzAnalyzer\                         the app (unpackaged, self-contained, x64)
#   dist\kHzAnalyzer-<version>-portable.zip   extract anywhere and run kHzAnalyzer.exe
#   dist\kHzAnalyzer-<version>-setup.exe      installer with a destination-folder page (needs Inno Setup 6)
# FFmpeg, yt-dlp and deno are copied from this machine into dist\kHzAnalyzer\tools.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$versao = ([xml](Get-Content "$raiz\KhzAnalyzer\KhzAnalyzer.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$saida = "$raiz\dist\kHzAnalyzer"

if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }
dotnet publish "$raiz\KhzAnalyzer\KhzAnalyzer.csproj" -c Release -r win-x64 -p:Platform=x64 -o $saida --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Get-ChildItem $saida -Filter *.pdb -Recurse | Remove-Item -Force

# ---- External tools used by Download & Trim and Snippets
$tools = "$saida\tools"
New-Item -ItemType Directory $tools -Force | Out-Null

$ffLink = Get-Item (Get-Command ffmpeg -ErrorAction Stop).Source
$ffReal = if ($ffLink.Target) { [string]$ffLink.Target } else { $ffLink.FullName }
Copy-Item $ffReal "$tools\ffmpeg.exe"
$ffRaiz = Split-Path (Split-Path $ffReal -Parent) -Parent
if (-not (Test-Path "$ffRaiz\LICENSE")) { throw "FFmpeg LICENSE not found next to the build; it must ship with ffmpeg.exe" }
Copy-Item "$ffRaiz\LICENSE" "$tools\FFmpeg-LICENSE.txt"
if (Test-Path "$ffRaiz\README.txt") { Copy-Item "$ffRaiz\README.txt" "$tools\FFmpeg-README.txt" }

$yt = (Get-Command yt-dlp -ErrorAction Stop).Source
Copy-Item $yt "$tools\yt-dlp.exe"
$deno = Join-Path (Split-Path $yt -Parent) 'deno.exe'
if (Test-Path $deno) { Copy-Item $deno "$tools\deno.exe" }

$ffVer = (& "$tools\ffmpeg.exe" -version | Select-Object -First 1)
$ytVer = (& "$tools\yt-dlp.exe" --version)
@"
Third-party programs in this folder
====================================
They are separate programs. kHz Analyzer starts them; it does not include their code.

FFmpeg  - $ffVer
          Licence: GNU General Public License version 3 (see FFmpeg-LICENSE.txt)
          Build: https://www.gyan.dev/ffmpeg/builds/   Source code: https://ffmpeg.org/download.html

yt-dlp  - version $ytVer
          Licence: The Unlicense   Source code: https://github.com/yt-dlp/yt-dlp

deno    - JavaScript runtime that yt-dlp uses for some sites (present only if it was available at build time)
          Licence: MIT   Source code: https://github.com/denoland/deno
"@ | Set-Content "$tools\THIRD-PARTY.txt" -Encoding utf8

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
