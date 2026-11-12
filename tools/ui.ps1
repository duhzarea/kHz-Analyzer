param([string]$Theme='Light',[string]$Dir='G:\#kHz Analyzer\tools\shots')
Add-Type -AssemblyName System.Drawing,UIAutomationClient,UIAutomationTypes
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W2{[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool r);
[DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);[DllImport("user32.dll")]public static extern void mouse_event(int f,int x,int y,int d,int e);public struct R{public int L,T,Rt,B;}}
"@
$exe='G:\#kHz Analyzer\KhzAnalyzer\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\kHzAnalyzer.exe'
$sp="$env:LOCALAPPDATA\kHzAnalyzer\settings.json"
$s=Get-Content $sp -Raw|ConvertFrom-Json; $s.Theme=$Theme; $s|ConvertTo-Json -Depth 5|Set-Content $sp -Encoding UTF8
Get-Process kHzAnalyzer -ErrorAction SilentlyContinue|Stop-Process -Force; Start-Sleep 1
$p=Start-Process $exe -PassThru
for($i=0;$i -lt 40 -and $p.MainWindowHandle -eq 0;$i++){Start-Sleep -Milliseconds 500;$p.Refresh()}
$h=$p.MainWindowHandle; [W2]::MoveWindow($h,40,40,1480,940,$true)|Out-Null; [W2]::SetForegroundWindow($h)|Out-Null; [W2]::SetWindowPos($h,[IntPtr](-1),0,0,0,0,3)|Out-Null; Start-Sleep 2
$root=[Windows.Automation.AutomationElement]::FromHandle($h)
function Shot($name){ $r=New-Object W2+R;[W2]::GetWindowRect($h,[ref]$r)|Out-Null
 $b=New-Object Drawing.Bitmap ($r.Rt-$r.L),($r.B-$r.T);$g=[Drawing.Graphics]::FromImage($b)
 $g.CopyFromScreen($r.L,$r.T,0,0,$b.Size);$b.Save("$Dir\$name.png",[Drawing.Imaging.ImageFormat]::Png);"shot $name" }
function Find($n){ $c=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$n)
 $root.FindFirst([Windows.Automation.TreeScope]::Descendants,$c) }
function Click($n){ $e=Find $n; if(-not $e){"MISSING: $n";return}
 try{$e.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()}catch{
 try{$e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()}catch{"no pattern: $n"}}; Start-Sleep 2 }
function Hover($n){ $e=Find $n; if(-not $e){"MISSING: $n";return}; $b=$e.Current.BoundingRectangle
 "$n rect $b"; $x=[int]($b.X+$b.Width/2);$y=[int]($b.Y+$b.Height/2); [W2]::SetCursorPos($x-30,$y)|Out-Null; for($k=0;$k -lt 6;$k++){[W2]::mouse_event(1,5,0,0,0);Start-Sleep -Milliseconds 40}; Start-Sleep -Milliseconds 900 }
$t=$Theme.ToLower()
[W2]::SetCursorPos(5,5)|Out-Null; Shot "a-$t-analyzer"
Hover 'Analyze'; Shot "a-$t-hover-analyze"
Hover 'Browse'; Shot "a-$t-hover-browse"
[W2]::SetCursorPos(5,5)|Out-Null
foreach($pg in 'Download & Trim','Snippets','About','Settings'){ Click $pg; [W2]::SetCursorPos(5,5)|Out-Null; Shot ("a-$t-"+($pg -replace '[^A-Za-z]',''))}
$p|Stop-Process -Force


