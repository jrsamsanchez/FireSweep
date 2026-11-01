# Draws icon.ico (an orange rounded square with a white TV) for build.ps1 to embed in the exe.
Add-Type -AssemblyName System.Drawing
$sizes = 256, 48, 32, 16
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object Drawing.Bitmap $s, $s
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $r = $s * 0.22; $d = $r * 2; $m = [math]::Max(0, $s * 0.02); $w = $s - 2 * $m
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc($m, $m, $d, $d, 180, 90); $path.AddArc($m + $w - $d, $m, $d, $d, 270, 90)
    $path.AddArc($m + $w - $d, $m + $w - $d, $d, $d, 0, 90); $path.AddArc($m, $m + $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(242, 95, 28))), $path)
    # The TV: a white screen outline and a stand.
    $pen = New-Object Drawing.Pen ([Drawing.Color]::White), ([math]::Max(1.5, $s * 0.07))
    $pen.LineJoin = 'Round'
    $g.DrawRectangle($pen, [float]($s * 0.2), [float]($s * 0.25), [float]($s * 0.6), [float]($s * 0.4))
    $g.DrawLine($pen, [float]($s * 0.36), [float]($s * 0.78), [float]($s * 0.64), [float]($s * 0.78))
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    , $ms.ToArray()
}
$out = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'icon.ico'), $out.ToArray())
