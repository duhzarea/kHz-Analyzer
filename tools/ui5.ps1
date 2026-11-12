$src=Get-Content 'G:\#kHz Analyzer\tools\ui2.ps1' -Raw
$head=$src.Substring(0,$src.IndexOf("Start-Sleep 2.6; Shot 'c-light-start'"))
$Dir='G:\#kHz Analyzer\tools\shots'
Invoke-Expression ($head -replace '^param\([^\n]*\n','')
Start-Sleep 3
foreach($k in '^1','^2','^3','^0','^b','^,'){ Keys $k; Start-Sleep 1.2; "sent $k" }
Start-Sleep 1; $p|Stop-Process -Force
Get-Content "$env:LOCALAPPDATA\kHzAnalyzer\logs\*" -Tail 40 | Select-String 'Shortcut' | % Line
