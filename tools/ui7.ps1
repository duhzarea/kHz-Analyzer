param([string]$Theme='Dark',[string]$Dir='G:\#kHz Analyzer\tools\shots\v7')
# Start screen (glass hub tiles) and Analyzer (sidebar theme switch + player scrubber bead).
Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W7{[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool r);
[DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint f);
public struct R{public int L,T,Rt,B;}}
"@
New-Item -ItemType Directory -Force $Dir | Out-Null
$exe='G:\#kHz Analyzer\KhzAnalyzer\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\kHzAnalyzer.exe'
$sp="$env:LOCALAPPDATA\kHzAnalyzer\settings.json"
$s=Get-Content $sp -Raw|ConvertFrom-Json; $s.Theme=$Theme; $s|ConvertTo-Json -Depth 5|Set-Content $sp -Encoding UTF8
Get-Process kHzAnalyzer -EA SilentlyContinue|Stop-Process -Force; Start-Sleep 1
$p=Start-Process $exe -PassThru
for($i=0;$i -lt 40 -and $p.MainWindowHandle -eq 0;$i++){Start-Sleep -Milliseconds 500;$p.Refresh()}
$h=$p.MainWindowHandle
[W7]::MoveWindow($h,40,40,1480,940,$true)|Out-Null; Start-Sleep 4
function Shot($n){ $r=New-Object W7+R;[W7]::GetWindowRect($h,[ref]$r)|Out-Null
  $b=New-Object Drawing.Bitmap ($r.Rt-$r.L),($r.B-$r.T);$g=[Drawing.Graphics]::FromImage($b)
  $dc=$g.GetHdc();[W7]::PrintWindow($h,$dc,2)|Out-Null;$g.ReleaseHdc($dc)
  $b.Save("$Dir\$n.png",[Drawing.Imaging.ImageFormat]::Png);"shot $n" }
function Crop($src,$dst,$x,$y,$w2,$h2,$z){
  $b=[Drawing.Bitmap]::FromFile("$Dir\$src.png")
  $r=New-Object Drawing.Rectangle $x,$y,$w2,$h2
  $c=$b.Clone($r,$b.PixelFormat)
  $o=New-Object Drawing.Bitmap ($w2*$z),($h2*$z)
  $g=[Drawing.Graphics]::FromImage($o)
  $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
  $g.DrawImage($c,0,0,($w2*$z),($h2*$z))
  $o.Save("$Dir\$dst.png",[Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); "crop $dst" }
$root=[Windows.Automation.AutomationElement]::FromHandle($h)
function Find($name){ $c=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$name)
  $root.FindFirst([Windows.Automation.TreeScope]::Descendants,$c) }
Shot "1-start-$Theme"
$t=Find 'Open Analyzer'
if($t){ ($t.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep 4.5; Shot "2-analyzer-$Theme"; Crop "2-analyzer-$Theme" "3-player-$Theme" 1050 480 420 200 3 } else { 'analyzer tile not found' }
$p|Stop-Process -Force
Get-ChildItem "$env:LOCALAPPDATA\kHzAnalyzer\logs" | Sort-Object LastWriteTime | Select-Object -Last 1 |
  Get-Content -Tail 40 | Select-String 'Tactile glass|Exception' | % Line
