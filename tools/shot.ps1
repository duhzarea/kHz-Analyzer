param([string]$Theme='Light',[string]$Out='G:\#kHz Analyzer\tools\shots\a-light.png',[int]$HoverX=-1,[int]$HoverY=-1,[switch]$Keep)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W{[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern bool MoveWindow(IntPtr h,int x,int y,int w,int ht,bool r);
public struct R{public int L,T,Rt,B;}}
"@
$exe='G:\#kHz Analyzer\KhzAnalyzer\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\kHzAnalyzer.exe'
$sp="$env:LOCALAPPDATA\kHzAnalyzer\settings.json"
$s=Get-Content $sp -Raw|ConvertFrom-Json; $s.Theme=$Theme; $s|ConvertTo-Json -Depth 5|Set-Content $sp -Encoding UTF8
Get-Process kHzAnalyzer -ErrorAction SilentlyContinue|Stop-Process -Force; Start-Sleep 1
$p=Start-Process $exe -PassThru
for($i=0;$i -lt 40 -and $p.MainWindowHandle -eq 0;$i++){Start-Sleep -Milliseconds 500;$p.Refresh()}
$h=$p.MainWindowHandle
[W]::MoveWindow($h,40,40,1480,940,$true)|Out-Null
[W]::SetForegroundWindow($h)|Out-Null; Start-Sleep 3
if($HoverX -ge 0){[W]::SetCursorPos(40+$HoverX,40+$HoverY)|Out-Null;Start-Sleep -Milliseconds 700}else{[W]::SetCursorPos(5,5)|Out-Null}
$r=New-Object W+R;[W]::GetWindowRect($h,[ref]$r)|Out-Null
$w=$r.Rt-$r.L;$ht=$r.B-$r.T
$bmp=New-Object Drawing.Bitmap $w,$ht;$g=[Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L,$r.T,0,0,$bmp.Size);New-Item -ItemType Directory -Force (Split-Path $Out)|Out-Null
$bmp.Save($Out,[Drawing.Imaging.ImageFormat]::Png);"$Out $w x $ht"
if(-not $Keep){$p|Stop-Process -Force}
