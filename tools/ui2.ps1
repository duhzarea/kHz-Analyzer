param([string]$Dir='G:\#kHz Analyzer\tools\shots',[string]$Folder='G:\NOT SHARE')
Add-Type -AssemblyName System.Drawing,System.Windows.Forms,UIAutomationClient,UIAutomationTypes
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W4{[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern void mouse_event(int f,int x,int y,int d,int e);
[DllImport("user32.dll")]public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool r);
[DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);
public struct R{public int L,T,Rt,B;}}
"@
$A=[Windows.Automation.AutomationElement]; $CT=[Windows.Automation.ControlType]
$exe='G:\#kHz Analyzer\KhzAnalyzer\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\kHzAnalyzer.exe'
$sp="$env:LOCALAPPDATA\kHzAnalyzer\settings.json"
$s=Get-Content $sp -Raw|ConvertFrom-Json; $s.Theme='Light'; $s|ConvertTo-Json -Depth 5|Set-Content $sp -Encoding UTF8
Get-Process kHzAnalyzer -EA SilentlyContinue|Stop-Process -Force; Start-Sleep 1
$p=Start-Process $exe -PassThru
for($i=0;$i -lt 40 -and $p.MainWindowHandle -eq 0;$i++){Start-Sleep -Milliseconds 500;$p.Refresh()}
$h=$p.MainWindowHandle; [W4]::MoveWindow($h,40,40,1480,940,$true)|Out-Null; [W4]::SetForegroundWindow($h)|Out-Null; [W4]::SetWindowPos($h,[IntPtr](-1),0,0,0,0,3)|Out-Null
[W4]::SetCursorPos(5,5)|Out-Null
$root=$A::FromHandle($h)
function Shot($name){ $r=New-Object W4+R;[W4]::GetWindowRect($h,[ref]$r)|Out-Null
 $b=New-Object Drawing.Bitmap ($r.Rt-$r.L),($r.B-$r.T);$g=[Drawing.Graphics]::FromImage($b)
 $g.CopyFromScreen($r.L,$r.T,0,0,$b.Size);$b.Save("$Dir\$name.png",[Drawing.Imaging.ImageFormat]::Png);"shot $name" }
function Ctl($n){ $c=New-Object Windows.Automation.PropertyCondition($A::NameProperty,$n)
 $e=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,$c); $w=[Windows.Automation.TreeWalker]::ControlViewWalker
 for($j=0;$j -lt 5 -and $e;$j++){ $t=$e.Current.ControlType; if($t -eq $CT::Button -or $t -eq $CT::RadioButton -or $t -eq $CT::ListItem -or $t -eq $CT::Edit){return $e}; $e=$w.GetParent($e) }; $null }
function Invoke($n){ $e=Ctl $n; if(-not $e){"MISSING: $n";return}
 foreach($pt in [Windows.Automation.InvokePattern]::Pattern,[Windows.Automation.SelectionItemPattern]::Pattern){ try{$o=$e.GetCurrentPattern($pt); if($pt -eq [Windows.Automation.InvokePattern]::Pattern){$o.Invoke()}else{$o.Select()}; break}catch{} } }
function Center($e){ $b=$e.Current.BoundingRectangle; @([int]($b.X+$b.Width/2),[int]($b.Y+$b.Height/2)) }
function HoverAt($x,$y){ [W4]::SetCursorPos($x-40,$y)|Out-Null; for($k=0;$k -lt 8;$k++){[W4]::mouse_event(1,5,0,0,0);Start-Sleep -Milliseconds 30} }
function Keys($k){ [W4]::SetForegroundWindow($h)|Out-Null; [System.Windows.Forms.SendKeys]::SendWait($k) }

Start-Sleep 2.6; Shot 'c-light-start'
Start-Sleep 3.8; Shot 'c-light-start-flip'
$t=Ctl 'Open Analyzer'; $c=Center $t; HoverAt $c[0] $c[1]; Start-Sleep -Milliseconds 700; Shot 'c-light-hover-tile'; [W4]::SetCursorPos(5,5)|Out-Null
Invoke 'Open Analyzer'; Start-Sleep 1.5
$ed=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition($A::NameProperty,'Folder')))
$ed.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Folder); Start-Sleep 1
Keys '{F5}'; Start-Sleep 6; Shot 'c-light-analyzer'
# play the first row with Enter
$list=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty,$CT::List)))
$first=$list.FindFirst([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty,$CT::ListItem)))
$first.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); $first.SetFocus(); Start-Sleep 0.5
Keys '{ENTER}'; Start-Sleep 4; Shot 'c-light-playing'
$c=Center $first; HoverAt ($c[0]-200) $c[1]; Start-Sleep 1.6; Shot 'c-light-tooltip'
[W4]::SetCursorPos($c[0]-200,$c[1])|Out-Null; [W4]::mouse_event(8,0,0,0,0); [W4]::mouse_event(16,0,0,0,0); Start-Sleep 0.8; Shot 'c-light-menu'; Keys '{ESC}'; [W4]::SetCursorPos(5,5)|Out-Null; Start-Sleep 0.5
Keys '^b'; Start-Sleep 0.8; Shot 'c-light-collapsed'; Keys '^b'; Start-Sleep 0.8
# theme switch with the reveal: capture mid-flight
$tt=Ctl 'Switch light and dark theme'; $c=Center $tt; [W4]::SetCursorPos($c[0],$c[1])|Out-Null; [W4]::mouse_event(2,0,0,0,0); [W4]::mouse_event(4,0,0,0,0)
Start-Sleep -Milliseconds 380; Shot 'c-theme-mid'; [W4]::SetCursorPos(5,5)|Out-Null; Start-Sleep 1.2; Shot 'c-dark-analyzer'
Keys '^,'; Start-Sleep 1.2; Shot 'c-dark-settings'
Keys '^/'; Start-Sleep 1.2; Shot 'c-dark-shortcuts'
Keys ' '; Start-Sleep 0.4
Keys '^0'; Start-Sleep 2.8; Shot 'c-dark-start'
"player check: " + ((Get-Content "$env:LOCALAPPDATA\kHzAnalyzer\logs\*" -Tail 3 -EA SilentlyContinue) -join ' | ')
$p|Stop-Process -Force
