<#
.SYNOPSIS
Generates TileShop.UI.Controls/Resources/AppIcons.Lucide.cs from Lucide SVG icons.

.DESCRIPTION
Each entry in $icons is either a Lucide icon (optionally with extra hand-authored path data
composited on top) or a fully hand-authored path on the same 24x24 grid. All SVG shape elements
are flattened into one SVG path string that Avalonia's StreamGeometry.Parse understands.

.PARAMETER PreviewFile
Optional path to also write an HTML contact sheet of every icon in every button state.
#>
param(
    [string]$LucideVersion = '1.47.0',
    [string]$OutFile = (Join-Path $PSScriptRoot '..\TileShop.UI.Controls\Resources\AppIcons.Lucide.cs'),
    [string]$PreviewFile
)

$ErrorActionPreference = 'Stop'
$culture = [System.Globalization.CultureInfo]::InvariantCulture

# Name = generated C# member. Lucide = source icon. Extra = path data drawn on top of the Lucide icon.
# Path = fully hand-authored icon (no Lucide source). All coordinates are on Lucide's 24x24 grid.
$icons = @(
    # Edit modes
    @{ Name = 'ModeView';        Lucide = 'eye' }
    @{ Name = 'ModeArrange';     Lucide = 'layout-grid' }
    @{ Name = 'ModeDraw';        Lucide = 'pencil' }

    # Selection
    @{ Name = 'SelectElements';  Lucide = 'square-mouse-pointer'; Extra = 'M3 12h9 M12 3v9' }
    @{ Name = 'SelectPixels';    Lucide = 'square-mouse-pointer'; Extra = 'M7.5 7.5h3v3h-3z' }

    # Arrange tools
    @{ Name = 'ApplyPalette';    Lucide = 'paint-roller' }
    @{ Name = 'PickPalette';     Lucide = 'pipette'; Extra = 'M15 18h4v4h-4z' }
    @{ Name = 'InspectElement';  Lucide = 'scan-eye' }
    @{ Name = 'RotateLeft';      Lucide = 'rotate-ccw' }
    @{ Name = 'RotateRight';     Lucide = 'rotate-cw' }
    @{ Name = 'MirrorHorizontal'; Lucide = 'flip-horizontal' }
    @{ Name = 'MirrorVertical';  Lucide = 'flip-vertical' }

    # Draw tools
    @{ Name = 'Pencil';          Lucide = 'pencil' }
    @{ Name = 'ColorPicker';     Lucide = 'pipette' }
    @{ Name = 'FloodFill';       Lucide = 'paint-bucket' }
    @{ Name = 'DrawClip';        Lucide = 'crop' }

    # Palette
    @{ Name = 'AddPalette';      Lucide = 'swatch-book' }
    @{ Name = 'EditColor';       Lucide = 'sliders-horizontal' }
    @{ Name = 'RemapColors';     Lucide = 'replace' }

    # Options / actions
    @{ Name = 'Gridlines';       Lucide = 'frame' }
    @{ Name = 'SnapToElements';  Lucide = 'magnet' }
    @{ Name = 'ResizeArranger';  Lucide = 'scaling' }

    # General
    @{ Name = 'Plus';            Lucide = 'plus' }
    @{ Name = 'Trash';           Lucide = 'trash-2' }
    @{ Name = 'CheckedBox';      Lucide = 'square-check' }
    @{ Name = 'UncheckedBox';    Lucide = 'square' }
    @{ Name = 'ChevronDown';     Lucide = 'chevron-down' }
    @{ Name = 'FolderOpen';      Lucide = 'folder-open' }

    # Zoom
    @{ Name = 'ZoomIn';          Lucide = 'zoom-in' }
    @{ Name = 'ZoomOut';         Lucide = 'zoom-out' }
    @{ Name = 'FitToView';       Lucide = 'maximize' }

    # Import preview modes
    @{ Name = 'ViewCurrent';     Lucide = 'image' }
    @{ Name = 'ViewImported';    Lucide = 'image-down' }
    @{ Name = 'ViewOnionSkin';   Lucide = 'layers-2' }
    @{ Name = 'ViewDiff';        Lucide = 'diff' }
)

function Format-Number([double]$value)
{
    return [Math]::Round($value, 3).ToString('0.###', $culture)
}

function ConvertTo-PathData([System.Xml.XmlElement]$element)
{
    function Attr([string]$name, [double]$default = 0)
    {
        $raw = $element.GetAttribute($name)
        if ([string]::IsNullOrWhiteSpace($raw)) { return $default }
        return [double]::Parse($raw, $culture)
    }

    switch ($element.LocalName)
    {
        'path' { return $element.GetAttribute('d') }
        'line' { return "M$(Format-Number (Attr 'x1')) $(Format-Number (Attr 'y1'))L$(Format-Number (Attr 'x2')) $(Format-Number (Attr 'y2'))" }
        'polyline' { return "M$($element.GetAttribute('points').Trim())" }
        'polygon' { return "M$($element.GetAttribute('points').Trim())Z" }
        'circle'
        {
            $cx = Attr 'cx'; $cy = Attr 'cy'; $r = Attr 'r'
            return "M$(Format-Number ($cx - $r)) $(Format-Number $cy)a$(Format-Number $r) $(Format-Number $r) 0 1 0 $(Format-Number (2 * $r)) 0a$(Format-Number $r) $(Format-Number $r) 0 1 0 $(Format-Number (-2 * $r)) 0Z"
        }
        'ellipse'
        {
            $cx = Attr 'cx'; $cy = Attr 'cy'; $rx = Attr 'rx'; $ry = Attr 'ry'
            return "M$(Format-Number ($cx - $rx)) $(Format-Number $cy)a$(Format-Number $rx) $(Format-Number $ry) 0 1 0 $(Format-Number (2 * $rx)) 0a$(Format-Number $rx) $(Format-Number $ry) 0 1 0 $(Format-Number (-2 * $rx)) 0Z"
        }
        'rect'
        {
            $x = Attr 'x'; $y = Attr 'y'; $w = Attr 'width'; $h = Attr 'height'
            $rx = Attr 'rx'; $ry = Attr 'ry' $rx
            if ($rx -eq 0 -and $ry -eq 0)
            {
                return "M$(Format-Number $x) $(Format-Number $y)h$(Format-Number $w)v$(Format-Number $h)h$(Format-Number (-$w))Z"
            }
            $arc = "a$(Format-Number $rx) $(Format-Number $ry) 0 0 1"
            return "M$(Format-Number ($x + $rx)) $(Format-Number $y)" +
                "h$(Format-Number ($w - 2 * $rx))$arc $(Format-Number $rx) $(Format-Number $ry)" +
                "v$(Format-Number ($h - 2 * $ry))$arc $(Format-Number (-$rx)) $(Format-Number $ry)" +
                "h$(Format-Number (2 * $rx - $w))$arc $(Format-Number (-$rx)) $(Format-Number (-$ry))" +
                "v$(Format-Number (2 * $ry - $h))$arc $(Format-Number $rx) $(Format-Number (-$ry))Z"
        }
        default { throw "Unsupported SVG element <$($element.LocalName)>" }
    }
}

# Normalizes the path data of one SVG element so it can be concatenated with others: every number
# is space-separated, every command is explicit (SVG allows ".5.5" and implicit repeats; Avalonia's
# parser is stricter), and a leading relative move becomes absolute since it was relative to (0,0).
function Format-PathData([string]$data)
{
    $arity = @{ M = 2; L = 2; T = 2; H = 1; V = 1; C = 6; S = 4; Q = 4; A = 7; Z = 0 }
    $tokens = @([regex]::Matches($data, '[MmZzLlHhVvCcSsQqTtAa]|[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?') | ForEach-Object { $_.Value })

    $out = [System.Collections.Generic.List[string]]::new()
    $command = $null
    $i = 0
    while ($i -lt $tokens.Count)
    {
        if ($tokens[$i] -match '[A-Za-z]')
        {
            $command = $tokens[$i]
            $emit = if ($out.Count -eq 0 -and $command -ceq 'm') { 'M' } else { $command }
            $i++
            if ($command -match '[Zz]') { $out.Add($command); continue }
        }
        else
        {
            $emit = switch -CaseSensitive ($command) { 'M' { 'L' } 'm' { 'l' } default { $command } }
            $command = $emit
        }

        $count = $arity[$emit.ToUpperInvariant()]
        $out.Add($emit)
        $out.AddRange([string[]]$tokens[$i..($i + $count - 1)])
        $i += $count
    }
    return $out -join ' '
}

function Get-LucidePathData([string]$name)
{
    $url = "https://cdn.jsdelivr.net/npm/lucide-static@$LucideVersion/icons/$name.svg"
    [xml]$svg = (Invoke-WebRequest -Uri $url -UseBasicParsing).Content
    return $svg.DocumentElement.ChildNodes |
        Where-Object { $_.NodeType -eq 'Element' } |
        ForEach-Object { Format-PathData (ConvertTo-PathData $_) }
}

$resolved = foreach ($icon in $icons)
{
    $parts = @()
    if ($icon.Lucide)
    {
        Write-Host "Fetching $($icon.Lucide) -> $($icon.Name)"
        $parts += Get-LucidePathData $icon.Lucide
    }
    if ($icon.Extra) { $parts += Format-PathData $icon.Extra }
    if ($icon.Path) { $parts += Format-PathData $icon.Path }

    [pscustomobject]@{
        Name = $icon.Name
        Source = if ($icon.Lucide) { $icon.Lucide } else { 'custom' }
        Data = $parts -join ' '
    }
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('// <auto-generated>')
[void]$sb.AppendLine("// Generated by tools/Convert-LucideIcons.ps1 from Lucide v$LucideVersion (ISC License, https://lucide.dev).")
[void]$sb.AppendLine('// Edit the icon table in the script and re-run it instead of editing this file.')
[void]$sb.AppendLine('// </auto-generated>')
[void]$sb.AppendLine('using Avalonia.Media;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('namespace TileShop.UI.Controls;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('public static partial class AppIcons')
[void]$sb.AppendLine('{')
foreach ($icon in $resolved)
{
    [void]$sb.AppendLine("    public static StreamGeometry $($icon.Name) { get; } = StreamGeometry.Parse(`"$($icon.Data)`"); // $($icon.Source)")
}
[void]$sb.AppendLine('}')

$outDir = Split-Path -Parent $OutFile
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
[System.IO.File]::WriteAllText((Resolve-Path -Path $outDir).Path + '\' + (Split-Path -Leaf $OutFile), $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $($resolved.Count) icons to $OutFile"

if ($PreviewFile)
{
    $states = @(
        @{ Name = 'idle';     Dark = 'rgba(249,249,249,0.62)'; Light = 'rgba(28,31,35,0.62)' }
        @{ Name = 'hover';    Dark = 'rgba(249,249,249,1)';    Light = 'rgba(28,31,35,1)' }
        @{ Name = 'checked';  Dark = '#54A9FF';                Light = '#0064FA' }
        @{ Name = 'disabled'; Dark = 'rgba(249,249,249,0.3)';  Light = 'rgba(28,31,35,0.3)' }
    )

    $svgOf = { param($data, $size, $color)
        "<svg width='$size' height='$size' viewBox='0 0 24 24' fill='none' stroke='$color' stroke-width='1.75' stroke-linecap='round' stroke-linejoin='round'><path d='$data'/></svg>" }

    $rows = foreach ($icon in $resolved)
    {
        $cells = foreach ($theme in 'Dark', 'Light')
        {
            foreach ($state in $states)
            {
                $bg = if ($state.Name -eq 'checked') { 'checked' } elseif ($state.Name -eq 'hover') { 'hover' } else { '' }
                "<td class='$($theme.ToLower())'><span class='btn $bg'>$(& $svgOf $icon.Data 20 $state[$theme])</span></td>"
            }
        }
        "<tr><th>$($icon.Name)<small>$($icon.Source)</small></th><td class='dark big'>$(& $svgOf $icon.Data 72 $states[1].Dark)</td>$($cells -join '')</tr>"
    }

    $html = @"
<title>TileShop Tool Icons</title>
<style>
:root{--bg:#f5f6f8;--fg:#1c1f23;--muted:#6b7280;--rule:#dfe2e7;--accent:#0064fa}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--bg:#16181c;--fg:#f9f9f9;--muted:#9ca3af;--rule:#2e3238;--accent:#54a9ff}}
:root[data-theme="dark"]{--bg:#16181c;--fg:#f9f9f9;--muted:#9ca3af;--rule:#2e3238;--accent:#54a9ff}
body{background:var(--bg);color:var(--fg);font:13px/1.4 system-ui,-apple-system,"Segoe UI",sans-serif;padding:24px 16px 48px;margin:0}
h1{font-size:18px;font-weight:600;margin:0 0 4px;text-wrap:balance}
p{color:var(--muted);margin:0 0 20px;max-width:65ch}
.wrap{overflow-x:auto}
table{border-collapse:separate;border-spacing:0;font-variant-numeric:tabular-nums}
thead th{font-size:11px;font-weight:500;color:var(--muted);letter-spacing:.04em;text-transform:uppercase;padding:0 6px 8px;text-align:center}
thead th.group{border-bottom:1px solid var(--rule)}
tbody th{text-align:left;padding:0 20px 0 0;font-weight:500;white-space:nowrap}
tbody th small{display:block;color:var(--muted);font-weight:400}
td{padding:4px 6px;text-align:center}
tbody tr td:nth-child(2){padding-right:20px}
td.dark{background:#1c1f23}td.light{background:#ffffff}
tr td.dark:first-of-type,tr td.light:first-of-type{border-radius:0}
.btn{display:inline-flex;width:32px;height:32px;align-items:center;justify-content:center;border-radius:3px;vertical-align:middle}
td.dark .btn.hover{background:rgba(255,255,255,.16)}td.dark .btn.checked{background:rgba(255,255,255,.20)}
td.light .btn.hover{background:rgba(46,50,56,.09)}td.light .btn.checked{background:rgba(46,50,56,.13)}
svg{display:block}
</style>
<h1>TileShop tool icons</h1>
<p>Lucide v$LucideVersion on a 24-unit grid, 1.75px round-capped stroke, rendered at 20px inside 32px buttons. State colors are the Semi theme values the app uses: text&nbsp;2 idle, text&nbsp;0 hover, primary when checked, disabled text when disabled.</p>
<div class="wrap"><table>
<thead>
<tr><th></th><th></th><th class="group" colspan="4">Dark theme</th><th class="group" colspan="4">Light theme</th></tr>
<tr><th style="text-align:left">Icon</th><th>72px</th><th>idle</th><th>hover</th><th>checked</th><th>disabled</th><th>idle</th><th>hover</th><th>checked</th><th>disabled</th></tr>
</thead>
<tbody>$($rows -join "`n")</tbody></table></div>
"@
    [System.IO.File]::WriteAllText($PreviewFile, $html, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Wrote preview to $PreviewFile"
}
