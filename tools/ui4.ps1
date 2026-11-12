$src=Get-Content 'G:\#kHz Analyzer\tools\ui2.ps1' -Raw
$head=$src.Substring(0,$src.IndexOf("Start-Sleep 2.6; Shot 'c-light-start'"))
$Dir='G:\#kHz Analyzer\tools\shots'; $Folder='G:\NOT SHARE'
$log=Get-ChildItem "$env:LOCALAPPDATA\kHzAnalyzer\logs" | Sort LastWriteTime | Select -Last 1
$before=(Get-Content $log.FullName).Count
Invoke-Expression ($head -replace '^param\([^\n]*\n','')
Start-Sleep -Milliseconds 450; Shot 'e-intro-450ms'
Start-Sleep 2.5
Keys '^1'; Start-Sleep 1.4; Shot 'e-after-ctrl1'
$ed=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition($A::NameProperty,'Folder')))
$ed.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Folder); Start-Sleep 0.4
Keys '{F5}'; Start-Sleep 4
$list=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty,$CT::List)))
$items=$list.FindAll([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty,$CT::ListItem)))
$c=Center $items[3]; HoverAt ($c[0]-120) $c[1]; Start-Sleep 2.5; Shot 'e-tooltip'; [W4]::SetCursorPos(5,5)|Out-Null
Keys '^+l'; Start-Sleep -Milliseconds 300; Shot 'e-theme-300'; Start-Sleep -Milliseconds 150; Shot 'e-theme-450'; Start-Sleep 1; Shot 'e-theme-done'
Keys '^/'; Start-Sleep 1.8; Shot 'e-shortcuts'
$p|Stop-Process -Force
"--- log ---"; Get-Content $log.FullName | Select -Skip $before | Select-String 'Shortcut|Theme|Exception' | % { $_.Line.Substring(0,[Math]::Min(220,$_.Line.Length)) }
