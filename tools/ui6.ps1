param([switch]$NoInput,[string]$Theme='Dark',[string]$Dir='G:\#kHz Analyzer\tools\shots\v6')
# Start screen footer (rest + hover), Settings from the start screen, sidebar ruler, Home key back to start.
Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W6{[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool r);
[DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint f);
[DllImport("user32.dll")]public static extern void keybd_event(byte vk,byte sc,int f,int e);
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
[W6]::MoveWindow($h,40,40,1480,940,$true)|Out-Null
if(-not $NoInput){ [W6]::SetForegroundWindow($h)|Out-Null; [W6]::SetCursorPos(5,5)|Out-Null }; Start-Sleep 3.5
function Shot($n){ $r=New-Object W6+R;[W6]::GetWindowRect($h,[ref]$r)|Out-Null
  $b=New-Object Drawing.Bitmap ($r.Rt-$r.L),($r.B-$r.T);$g=[Drawing.Graphics]::FromImage($b)
  $dc=$g.GetHdc();[W6]::PrintWindow($h,$dc,2)|Out-Null;$g.ReleaseHdc($dc);$b.Save("$Dir\$n.png",[Drawing.Imaging.ImageFormat]::Png);"shot $n" }
$root=[Windows.Automation.AutomationElement]::FromHandle($h)
function Find($name){ $c=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$name)
  $root.FindFirst([Windows.Automation.TreeScope]::Descendants,$c) }
function Key($vk){ [W6]::keybd_event($vk,0,0,0); Start-Sleep -Milliseconds 40; [W6]::keybd_event($vk,0,2,0) }

Shot '1-start-rest'
$m=Find 'Crafted by DUHZZ, open GitHub profile'
if($m -and -not $NoInput){ $b=$m.Current.BoundingRectangle; [W6]::SetCursorPos([int]($b.X+$b.Width/2),[int]($b.Y+$b.Height/2))|Out-Null; Start-Sleep -Milliseconds 700; Shot '2-start-hover' } else { 'build mark not found' }
if(-not $NoInput){[W6]::SetCursorPos(5,5)|Out-Null}
$st=Find 'Settings'
if($st){ ($st.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke(); Start-Sleep 1.6; Shot '3-settings-from-start' } else { 'settings not found' }
if(-not $NoInput){ Key 0x24; Start-Sleep 1.6; Shot '4-after-home-key' }
$p|Stop-Process -Force
Get-ChildItem "$env:LOCALAPPDATA\kHzAnalyzer\logs" | Sort-Object LastWriteTime | Select-Object -Last 1 | Get-Content -Tail 30 | Select-String 'Shortcut|Exception|error' | % Line
