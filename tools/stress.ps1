param([string]$Theme='Dark')
# Walks the whole shell twice, switching theme, and reports whether the process survived.
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type @"
using System;using System.Runtime.InteropServices;
public class K{[DllImport("user32.dll")]public static extern void keybd_event(byte vk,byte sc,int f,int e);
[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);}
"@
$exe='G:\#kHz Analyzer\KhzAnalyzer\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\kHzAnalyzer.exe'
$sp="$env:LOCALAPPDATA\kHzAnalyzer\settings.json"
$s=Get-Content $sp -Raw|ConvertFrom-Json; $s.Theme=$Theme; $s|ConvertTo-Json -Depth 5|Set-Content $sp -Encoding UTF8
Get-Process kHzAnalyzer -EA SilentlyContinue|Stop-Process -Force; Start-Sleep 1
$p=Start-Process $exe -PassThru
for($i=0;$i -lt 40 -and $p.MainWindowHandle -eq 0;$i++){Start-Sleep -Milliseconds 500;$p.Refresh()}
$h=$p.MainWindowHandle
[K]::SetForegroundWindow($h)|Out-Null; Start-Sleep 4
$root=[Windows.Automation.AutomationElement]::FromHandle($h)
function Click($name){
  if($p.HasExited){ return $false }
  $c=New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$name)
  $e=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,$c)
  if($e){ try{ ($e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)).Invoke() }catch{ try{ ($e.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern)).Select() }catch{} }; Start-Sleep 1.4; return $true }
  "  miss: $name"; return $false }
function Key($vk){ [K]::keybd_event($vk,0,0,0); Start-Sleep -Milliseconds 40; [K]::keybd_event($vk,0,2,0); Start-Sleep 1.4 }

for($round=1; $round -le 2; $round++){
  "round $round"
  Click 'Open Analyzer' | Out-Null
  Click 'Download & Trim' | Out-Null
  Click 'Snippets' | Out-Null
  Click 'Settings' | Out-Null
  Click 'Switch light and dark theme' | Out-Null
  Key 0x24                      # Home, back to the start screen
  Click 'Open Snippets' | Out-Null
  Key 0x24
  $p.Refresh(); if($p.HasExited){ "CRASHED in round $round exit=$($p.ExitCode)"; exit 1 }
}
$p.Refresh()
if($p.HasExited){ "CRASHED exit=$($p.ExitCode)" } else { "OK survived both rounds"; $p|Stop-Process -Force }
