# Adds the painted bevel tokens to each theme dictionary, after that theme's GlassGradientBrush.
$f = 'G:\#kHz Analyzer\KhzAnalyzer\Styles\Tokens.xaml'
$t = [IO.File]::ReadAllText($f)

$lightOld = '<LinearGradientBrush x:Key="GlassGradientBrush" StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#9EFFFFFF" Offset="0" /><GradientStop Color="#66FFFFFF" Offset="1" /></LinearGradientBrush>'
$lightNew = $lightOld + @'

            <!-- Bevel: key light from the upper left, structural line through the middle, warm bounce
                 off the brand red at the lower right. Same model the hub tiles and the switch share. -->
            <LinearGradientBrush x:Key="BevelRimBrush" StartPoint="0.12,0" EndPoint="0.88,1"><GradientStop Color="#CCFFFFFF" Offset="0" /><GradientStop Color="#33A9C4DE" Offset="0.35" /><GradientStop Color="#260F172A" Offset="0.62" /><GradientStop Color="#59FA243C" Offset="1" /></LinearGradientBrush>
            <LinearGradientBrush x:Key="BevelInnerBrush" StartPoint="0.5,0" EndPoint="0.5,1"><GradientStop Color="#00FFFFFF" Offset="0" /><GradientStop Color="#1A0F172A" Offset="1" /></LinearGradientBrush>
'@

$darkOld = '<LinearGradientBrush x:Key="GlassGradientBrush" StartPoint="0,0" EndPoint="1,1"><GradientStop Color="#A6111827" Offset="0" /><GradientStop Color="#800D111C" Offset="1" /></LinearGradientBrush>'
$darkNew = $darkOld + @'

            <LinearGradientBrush x:Key="BevelRimBrush" StartPoint="0.12,0" EndPoint="0.88,1"><GradientStop Color="#F2FFFFFF" Offset="0" /><GradientStop Color="#4DFFFFFF" Offset="0.32" /><GradientStop Color="#14FFFFFF" Offset="0.6" /><GradientStop Color="#B3FF8A9B" Offset="1" /></LinearGradientBrush>
            <LinearGradientBrush x:Key="BevelInnerBrush" StartPoint="0.5,0" EndPoint="0.5,1"><GradientStop Color="#00000000" Offset="0" /><GradientStop Color="#380A0C11" Offset="1" /></LinearGradientBrush>
'@

$hcOld = '<SolidColorBrush x:Key="GlassGradientBrush" Color="{ThemeResource SystemColorWindowColor}" />'
$hcNew = $hcOld + @'

            <SolidColorBrush x:Key="BevelRimBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
            <SolidColorBrush x:Key="BevelInnerBrush" Color="{ThemeResource SystemColorWindowColor}" />
'@

foreach ($pair in @(@($lightOld, $lightNew), @($darkOld, $darkNew), @($hcOld, $hcNew))) {
    if (-not $t.Contains($pair[0])) { throw "anchor not found: $($pair[0].Substring(0,60))" }
    $t = $t.Replace($pair[0], $pair[1])
}
[IO.File]::WriteAllText($f, $t, (New-Object Text.UTF8Encoding $true))
(Select-String -Path $f -Pattern 'BevelRimBrush|BevelInnerBrush').Count.ToString() + ' bevel brush lines written'
