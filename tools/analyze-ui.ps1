param([string]$Theme='Light',[string]$Dir='G:\#kHz Analyzer\tools\shots',[string]$Folder='G:\NOT SHARE')
Add-Type -AssemblyName System.Drawing,UIAutomationClient,UIAutomationTypes
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W3{[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool r);
[DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);
public struct R{public int L,T,Rt,B;}}
"@
$A=[Windows.Automation.AutomationElement]; $CT=[Windows.Automation.ControlType]
$exe='G:\#kHz Analyzer\KhzAnalyzer\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\kHzAnalyzer.exe'
$sp="$env:LOCALAPPDATA\kHzAnalyzer\settings.json"
$s=Get-Content $sp -Raw|ConvertFrom-Json; $s.Theme=$Theme; $s|ConvertTo-Json -Depth 5|Set-Content $sp -Encoding UTF8
Get-Process kHzAnalyzer -ErrorAction SilentlyContinue|Stop-Process -Force; Start-Sleep 1
$p=Start-Process $exe -PassThru
for($i=0;$i -lt 40 -and $p.MainWindowHandle -eq 0;$i++){Start-Sleep -Milliseconds 500;$p.Refresh()}
$h=$p.MainWindowHandle; [W3]::MoveWindow($h,40,40,1480,940,$true)|Out-Null; [W3]::SetForegroundWindow($h)|Out-Null; [W3]::SetWindowPos($h,[IntPtr](-1),0,0,0,0,3)|Out-Null; Start-Sleep 2
$root=$A::FromHandle($h)
function Shot($name){ $r=New-Object W3+R;[W3]::GetWindowRect($h,[ref]$r)|Out-Null
 $b=New-Object Drawing.Bitmap ($r.Rt-$r.L),($r.B-$r.T);$g=[Drawing.Graphics]::FromImage($b)
 $g.CopyFromScreen($r.L,$r.T,0,0,$b.Size);$b.Save("$Dir\$name.png",[Drawing.Imaging.ImageFormat]::Png);"shot $name" }
function Ctl($n){ $c=New-Object Windows.Automation.PropertyCondition($A::NameProperty,$n)
 $e=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,$c); $w=[Windows.Automation.TreeWalker]::ControlViewWalker
 for($j=0;$j -lt 5 -and $e;$j++){ $t=$e.Current.ControlType; if($t -eq $CT::Button -or $t -eq $CT::RadioButton -or $t -eq $CT::ListItem){return $e}; $e=$w.GetParent($e) }; $null }
function Click($n){ $e=Ctl $n; if(-not $e){"MISSING: $n";return}
 foreach($pt in [Windows.Automation.InvokePattern]::Pattern,[Windows.Automation.SelectionItemPattern]::Pattern){ try{$o=$e.GetCurrentPattern($pt); if($pt -eq [Windows.Automation.InvokePattern]::Pattern){$o.Invoke()}else{$o.Select()}; break}catch{} }; Start-Sleep 2 }
$t=$Theme.ToLower(); [W3]::SetCursorPos(5,5)|Out-Null
$ed=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty,$CT::Edit)))
$ed.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Folder); Start-Sleep 2; "path box: $($ed.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value)"
$cancel=Ctl 'Cancel'; "cancel enabled before: $($cancel.Current.IsEnabled)"
$sw=[Diagnostics.Stopwatch]::StartNew(); Click 'Analyze'; "cancel enabled after click: $($cancel.Current.IsEnabled)"
$shot=$false
for($k=0;$k -lt 2400;$k++){ Start-Sleep 1; if(-not $cancel.Current.IsEnabled){break}; if(-not $shot -and $k -ge 25){Shot "a-$t-running";$shot=$true} }
"analysis seconds: $([int]$sw.Elapsed.TotalSeconds)"; Start-Sleep 2; Shot "a-$t-done"
Click 'Issues'; Shot "a-$t-issues"
$p|Stop-Process -Force
