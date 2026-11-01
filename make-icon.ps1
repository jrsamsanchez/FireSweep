# Draws the FireSweep logo: a white flame with a sparkle on an orange-to-magenta tile.
# Writes logo.png (512 px, used in the app and the README) and icon.ico (the exe's icon).
Add-Type -AssemblyName System.Drawing

function Draw-Logo([int]$s) {
    $bmp = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    function P([double]$x, [double]$y) { New-Object Drawing.PointF ([float]($x * $s)), ([float]($y * $s)) }

    # Rounded tile with a diagonal gradient.
    $m = [math]::Max(0.5, $s * 0.02); $w = $s - 2 * $m; $d = $w * 0.46
    $tile = New-Object Drawing.Drawing2D.GraphicsPath
    $tile.AddArc($m, $m, $d, $d, 180, 90); $tile.AddArc($m + $w - $d, $m, $d, $d, 270, 90)
    $tile.AddArc($m + $w - $d, $m + $w - $d, $d, $d, 0, 90); $tile.AddArc($m, $m + $w - $d, $d, $d, 90, 90)
    $tile.CloseFigure()
    $grad = New-Object Drawing.Drawing2D.LinearGradientBrush (P 0 0), (P 1 1), ([Drawing.Color]::FromArgb(255, 160, 40)), ([Drawing.Color]::FromArgb(226, 36, 110))
    $g.FillPath($grad, $tile)

    # Flame.
    $flame = New-Object Drawing.Drawing2D.GraphicsPath
    $flame.AddBezier((P .50 .86), (P .31 .86), (P .20 .72), (P .22 .56))
    $flame.AddBezier((P .22 .56), (P .24 .42), (P .33 .36), (P .36 .25))
    $flame.AddBezier((P .36 .25), (P .42 .33), (P .43 .39), (P .44 .43))
    $flame.AddBezier((P .44 .43), (P .47 .30), (P .55 .20), (P .62 .11))
    $flame.AddBezier((P .62 .11), (P .66 .26), (P .80 .40), (P .80 .58))
    $flame.AddBezier((P .80 .58), (P .80 .74), (P .68 .86), (P .50 .86))
    $flame.CloseFigure()
    $g.FillPath([Drawing.Brushes]::White, $flame)

    # Inner flame, in the tile's colors so the flame reads as two-tone.
    if ($s -ge 32) {
        $inner = New-Object Drawing.Drawing2D.GraphicsPath
        $inner.AddBezier((P .50 .80), (P .41 .80), (P .36 .73), (P .37 .66))
        $inner.AddBezier((P .37 .66), (P .38 .58), (P .45 .53), (P .48 .45))
        $inner.AddBezier((P .48 .45), (P .54 .53), (P .63 .59), (P .63 .67))
        $inner.AddBezier((P .63 .67), (P .63 .76), (P .57 .80), (P .50 .80))
        $inner.CloseFigure()
        $innerGrad = New-Object Drawing.Drawing2D.LinearGradientBrush (P 0 .4), (P 0 .82), ([Drawing.Color]::FromArgb(255, 150, 45)), ([Drawing.Color]::FromArgb(240, 70, 80))
        $g.FillPath($innerGrad, $inner)
    }

    # Sparkle: a four-point star at the top right.
    if ($s -ge 24) {
        $cx = .80; $cy = .22; $r = .085; $k = .022
        $pts = [Drawing.PointF[]]@(
            (P $cx ($cy - $r)), (P ($cx + $k) ($cy - $k)), (P ($cx + $r) $cy), (P ($cx + $k) ($cy + $k)),
            (P $cx ($cy + $r)), (P ($cx - $k) ($cy + $k)), (P ($cx - $r) $cy), (P ($cx - $k) ($cy - $k)))
        $g.FillPolygon([Drawing.Brushes]::White, $pts)
    }
    $g.Dispose()
    $bmp
}

function Png([Drawing.Bitmap]$b) { $ms = New-Object IO.MemoryStream; $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png); , $ms.ToArray() }

$logo = Draw-Logo 512
$logo.Save((Join-Path $PSScriptRoot 'logo.png'), [Drawing.Imaging.ImageFormat]::Png)

$sizes = 256, 64, 48, 32, 24, 16
$pngs = foreach ($s in $sizes) { Png (Draw-Logo $s) }
$out = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $b = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'icon.ico'), $out.ToArray())
