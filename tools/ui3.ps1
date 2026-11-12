param([string]$Dir='G:\#kHz Analyzer\tools\shots',[string]$Folder='G:\NOT SHARE')
$src=Get-Content 'G:\#kHz Analyzer\tools\ui2.ps1' -Raw
$head=$src.Substring(0,$src.IndexOf("Start-Sleep 2.6; Shot 'c-light-start'"))
$log=Get-ChildItem "$env:LOCALAPPDATA\kHzAnalyzer\logs" | Sort LastWriteTime | Select -Last 1
$before=(Get-Content $log.FullName).Count
Invoke-Expression ($head -replace '^param\([^\n]*\n','')
Start-Sleep 2.8; Shot 'd-light-start'
Keys '^1'; Start-Sleep 1.2
$ed=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition($A::NameProperty,'Folder')))
$ed.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($Folder); Start-Sleep 0.5
Keys '{F5}'; Start-Sleep 5
$list=$root.FindFirst([Windows.Automation.TreeScope]::Descendants,(New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty,$CT::List)))
$items=$list.FindAll([Windows.Automation.TreeScope]::Children,(New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty,$CT::ListItem)))
$items[5].GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select(); $items[5].SetFocus(); Start-Sleep 0.4
Keys '{ENTER}'; Start-Sleep 5; Shot 'd-light-playing'
$c=Center $items[2]; HoverAt ($c[0]-150) $c[1]; Start-Sleep 2.0; Shot 'd-light-tooltip'; [W4]::SetCursorPos(5,5)|Out-Null
Keys '^+l'; Start-Sleep -Milliseconds 330; Shot 'd-theme-mid'; Start-Sleep 1.3; Shot 'd-dark-playing'
Keys ' '; Start-Sleep 1; "after space"
Invoke 'Switch light and dark theme'; Start-Sleep 1.6; Shot 'd-light-again'
Keys '^/'; Start-Sleep 1.6; Shot 'd-shortcuts'
Keys '^0'; Start-Sleep 3; Shot 'd-light-start2'
$p|Stop-Process -Force
"--- log ---"; Get-Content $log.FullName | Select -Skip $before | Select-String 'Shortcut|Theme|Play|Media|Exception' | % { $_.Line.Substring(0,[Math]::Min(220,$_.Line.Length)) }
