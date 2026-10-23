param([string]$Out, [string]$PreviewPng)
# Crea l'icona di WebDeployer: quadrato arrotondato viola (#5B3FD6), freccia "carica" bianca e una stellina.
Add-Type -AssemblyName System.Drawing

function RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw([int]$size) {
    $bmp = New-Object Drawing.Bitmap($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)
    $s = $size / 24.0
    $m = [Math]::Max(0.5, $size / 32.0)

    # Sfondo: viola del design con una leggera sfumatura verso il basso
    $rect = New-Object Drawing.RectangleF($m, $m, ($size - 2 * $m), ($size - 2 * $m))
    $path = RoundedRect $rect.X $rect.Y $rect.Width $rect.Height ($size * 0.22)
    $top = [Drawing.Color]::FromArgb(0x73, 0x58, 0xE6)
    $bottom = [Drawing.Color]::FromArgb(0x4A, 0x31, 0xBF)
    $brush = New-Object Drawing.Drawing2D.LinearGradientBrush($rect, $top, $bottom, 90)
    $g.FillPath($brush, $path)

    # Freccia "carica" come l'icona del pulsante "Build e carica"
    $pen = New-Object Drawing.Pen([Drawing.Color]::White, [Math]::Max(1.6, 2.1 * $s))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    function P([float]$x, [float]$y) { New-Object Drawing.PointF(($x * $s), ($y * $s)) }
    $g.DrawLine($pen, (P 12 5.5), (P 12 14.5))
    $g.DrawLines($pen, [Drawing.PointF[]]@((P 8 9.5), (P 12 5.5), (P 16 9.5)))
    $g.DrawLines($pen, [Drawing.PointF[]]@((P 6 14.5), (P 6 17.5), (P 7.5 19), (P 16.5 19), (P 18 17.5), (P 18 14.5)))

    # Stellina in alto a destra (solo dove si vede)
    if ($size -ge 32) {
        $cx = 19 * $s; $cy = 5 * $s; $r1 = 2.6 * $s; $r2 = 0.8 * $s
        $pts = New-Object 'System.Collections.Generic.List[Drawing.PointF]'
        for ($i = 0; $i -lt 8; $i++) {
            $a = [Math]::PI / 4 * $i - [Math]::PI / 2
            $r = if ($i % 2 -eq 0) { $r1 } else { $r2 }
            $pts.Add((New-Object Drawing.PointF(($cx + $r * [Math]::Cos($a)), ($cy + $r * [Math]::Sin($a)))))
        }
        $star = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(0xFF, 0xE8, 0x8C))
        $g.FillPolygon($star, $pts.ToArray())
    }
    $g.Dispose()
    return $bmp
}

# Immagine in formato DIB (bitmap 32 bit dal basso verso l'alto + maschera), letta da qualsiasi versione di Windows/.NET
function ToDib($bmp) {
    $size = $bmp.Width
    $ms = New-Object IO.MemoryStream
    $bw = New-Object IO.BinaryWriter($ms)
    $bw.Write([UInt32]40); $bw.Write([Int32]$size); $bw.Write([Int32]($size * 2))
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]0); $bw.Write([UInt32]0); $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
    $rect = New-Object Drawing.Rectangle(0, 0, $size, $size)
    $data = $bmp.LockBits($rect, 'ReadOnly', [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $size)
    [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bmp.UnlockBits($data)
    for ($y = $size - 1; $y -ge 0; $y--) { $bw.Write($bytes, $y * $data.Stride, $size * 4) }
    $maskRow = [int][Math]::Ceiling($size / 32.0) * 4
    $bw.Write((New-Object byte[] ($maskRow * $size)))
    $bw.Flush()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$pngs = foreach ($size in $sizes) {
    $bmp = Draw $size
    if ($size -ge 256) {
        $ms = New-Object IO.MemoryStream
        $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
        if ($PreviewPng) { $bmp.Save($PreviewPng) }
        $bytes = $ms.ToArray()
    } else {
        $bytes = ToDib $bmp
    }
    $bmp.Dispose()
    , $bytes
}

# Formato ICO: bitmap per le dimensioni piccole, PNG per 256 px
$fs = [IO.File]::Create($Out)
$w = New-Object IO.BinaryWriter($fs)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([Byte]$dim); $w.Write([Byte]$dim); $w.Write([Byte]0); $w.Write([Byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $w.Write($png) }
$w.Close()
"Icona scritta: $Out ($((Get-Item $Out).Length) byte, $($sizes.Count) dimensioni)"
