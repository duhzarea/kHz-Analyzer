param(
  [string]$File = 'G:\#kHz Analyzer\tests\fixtures\lowpass_8khz_16b44k.wav',
  [string]$Theme = 'Dark',
  [string]$Dir = 'G:\#kHz Analyzer\tools\shots\spec')
# Drops a file on the Analyzer dropzone through the file picker and shoots the spectrogram dialog.
Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes, System.Windows.Forms
Add-Type @"
using System;using System.Runtime.InteropServices;
public class WS{[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool r);
[DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint f);
[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern void mouse_event(uint f,uint x,uint y,uint d,IntPtr e);
[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
public struct R{public int L,T,Rt,B;}}
"@
New-Item -ItemType Directory -Force $Dir | Out-Null
$exe = 'G:\#kHz Analyzer\KhzAnalyzer\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\kHzAnalyzer.exe'
$sp = "$env:LOCALAPPDATA\kHzAnalyzer\settings.json"
$s = Get-Content $sp -Raw | ConvertFrom-Json; $s.Theme = $Theme; $s | ConvertTo-Json -Depth 5 | Set-Content $sp -Encoding UTF8
Get-Process kHzAnalyzer -EA SilentlyContinue | Stop-Process -Force; Start-Sleep 1
$p = Start-Process $exe -PassThru
for ($i = 0; $i -lt 40 -and $p.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 500; $p.Refresh() }
$h = $p.MainWindowHandle
[WS]::MoveWindow($h, 40, 40, 1480, 940, $true) | Out-Null; Start-Sleep 4
function Shot($n) {
  $r = New-Object WS+R; [WS]::GetWindowRect($h, [ref]$r) | Out-Null
  $b = New-Object Drawing.Bitmap ($r.Rt - $r.L), ($r.B - $r.T); $g = [Drawing.Graphics]::FromImage($b)
  $dc = $g.GetHdc(); [WS]::PrintWindow($h, $dc, 2) | Out-Null; $g.ReleaseHdc($dc)
  $b.Save("$Dir\$n.png", [Drawing.Imaging.ImageFormat]::Png); "shot $n"
}
$root = [Windows.Automation.AutomationElement]::FromHandle($h)
$c = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Open Analyzer')
$tile = $root.FindFirst([Windows.Automation.TreeScope]::Descendants, $c)
if (-not $tile) { 'analyzer tile not found'; exit 1 }
($tile.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep 4

# Dropzone sits under the folder row; click its middle.
[WS]::SetForegroundWindow($h) | Out-Null
[WS]::SetCursorPos(900, 286) | Out-Null; Start-Sleep 1
[WS]::mouse_event(0x02, 0, 0, 0, [IntPtr]::Zero); [WS]::mouse_event(0x04, 0, 0, 0, [IntPtr]::Zero)
Start-Sleep 3

# Native picker: type the path and confirm.
[System.Windows.Forms.SendKeys]::SendWait($File.Replace('(', '{(}').Replace(')', '{)}'))
Start-Sleep 1
[System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
Start-Sleep 12
Shot "spectrogram-$Theme"
Start-Sleep 1
$p | Stop-Process -Force
Get-ChildItem "$env:LOCALAPPDATA\kHzAnalyzer\logs" | Sort-Object LastWriteTime | Select-Object -Last 1 |
  Get-Content -Tail 30 | Select-String 'Exception|cannot be read' | ForEach-Object Line
