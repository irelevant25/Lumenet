# Draws the icon of Lumenet: three faders of the desk on a dark tile, their knobs in the colors
# of the desk's keys (channels green, the functions amber, memories red). It writes lumenet.ico
# beside this script (the exe, the tray and the log window use it) and the page's favicon.ico.
#
#   powershell -ExecutionPolicy Bypass -File backend\Lumenet\make-icon.ps1
#
# The small sizes are drawn on whole pixels, so that they stay sharp in the taskbar.

param(
    # Where previews of every size go as PNG files, to look at; none when not given.
    [string] $Previews = ''
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$icoFile = Join-Path $here 'lumenet.ico'
$favicon = Join-Path $here '..\..\frontend\public\favicon.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function Color([string] $hex) {
    return [System.Drawing.ColorTranslator]::FromHtml($hex)
}

# The colors of the desk (frontend/src/styles.css and desk/key-colors.ts).
$edge = Color '#59616e'
$tileTop = Color '#2b3038'
$tileBottom = Color '#16191d'
$slot = Color '#08090b'
$knobs = @(
    # x of the fader across the tile, top of its knob down the tile, its color
    @{ X = 0.28; Top = 0.60; Color = (Color '#3ddc84') },
    @{ X = 0.50; Top = 0.22; Color = (Color '#ffb020') },
    @{ X = 0.72; Top = 0.41; Color = (Color '#ff5247') }
)

function RoundedRectangle([single] $x, [single] $y, [single] $width, [single] $height, [single] $radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $radius
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $width - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $width - $d, $y + $height - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw([int] $size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([System.Drawing.Color]::Transparent)

    # The tile with an edge that shows on a dark taskbar too.
    $radius = [Math]::Max(3, [Math]::Round($size * 0.19))
    $edgeWidth = [Math]::Max(1, [Math]::Round($size / 32))
    $outer = RoundedRectangle 0 0 $size $size $radius
    $g.FillPath((New-Object System.Drawing.SolidBrush($edge)), $outer)
    $inner = RoundedRectangle $edgeWidth $edgeWidth ($size - 2 * $edgeWidth) ($size - 2 * $edgeWidth) ([Math]::Max(1, $radius - $edgeWidth))
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF(0, $size)), $tileTop, $tileBottom)
    $g.FillPath($fill, $inner)

    # The faders: a slot each, and a knob on it, on whole pixels so that a 16-pixel icon stays
    # sharp. The sizes are even, so even widths keep the middle fader in the middle; the outer two
    # stand as far from the edges.
    $slotWidth = [Math]::Max(2, 2 * [Math]::Round($size * 0.035))
    $knobWidth = [Math]::Max(4, 2 * [Math]::Round($size * 0.105))
    $knobHeight = [Math]::Max(2, [Math]::Round($size * 0.13))
    $slotTop = [Math]::Round($size * 0.17)
    $slotBottom = $size - $slotTop
    $outerMiddle = [Math]::Round($size * 0.28)
    foreach ($knob in $knobs) {
        $middle = if ($knob.X -lt 0.5) { $outerMiddle } elseif ($knob.X -gt 0.5) { $size - $outerMiddle } else { $size / 2 }
        $slotLeft = $middle - $slotWidth / 2
        $knobLeft = $middle - $knobWidth / 2
        $g.FillRectangle((New-Object System.Drawing.SolidBrush($slot)), [single] $slotLeft, [single] $slotTop, [single] $slotWidth, [single] ($slotBottom - $slotTop))

        $top = [Math]::Round($size * $knob.Top)
        if ($size -ge 40) {
            $knobPath = RoundedRectangle $knobLeft $top $knobWidth $knobHeight ([Math]::Max(1, $knobHeight * 0.3))
            $g.FillPath((New-Object System.Drawing.SolidBrush($knob.Color)), $knobPath)
            # a lit line across the knob, as on the faders of the desk
            $line = [Math]::Max(1, [Math]::Round($size / 64))
            $g.FillRectangle((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 255, 255, 255))),
                [single] ($knobLeft + $line), [single] ($top + ($knobHeight - $line) / 2), [single] ($knobWidth - 2 * $line), [single] $line)
        } else {
            $g.FillRectangle((New-Object System.Drawing.SolidBrush($knob.Color)), [single] $knobLeft, [single] $top, [single] $knobWidth, [single] $knobHeight)
        }
    }

    $g.Dispose()
    return $bitmap
}

# An image of the icon file: PNG for the largest size, the classic bitmap with a mask for the others.
function IconImage([System.Drawing.Bitmap] $bitmap) {
    $size = $bitmap.Width
    $stream = New-Object System.IO.MemoryStream
    if ($size -ge 256) {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return , $stream.ToArray()
    }

    $writer = New-Object System.IO.BinaryWriter($stream)
    $maskStride = [int] ([Math]::Floor(($size + 31) / 32) * 4)
    $writer.Write([int] 40)              # BITMAPINFOHEADER
    $writer.Write([int] $size)
    $writer.Write([int] ($size * 2))     # the colors and the mask
    $writer.Write([int16] 1)
    $writer.Write([int16] 32)
    $writer.Write([int] 0)
    $writer.Write([int] ($size * $size * 4 + $maskStride * $size))
    $writer.Write([int] 0); $writer.Write([int] 0); $writer.Write([int] 0); $writer.Write([int] 0)

    $mask = New-Object byte[] ($maskStride * $size)
    for ($row = $size - 1; $row -ge 0; $row--) {
        # bottom row first
        for ($x = 0; $x -lt $size; $x++) {
            $pixel = $bitmap.GetPixel($x, $row)
            $writer.Write([byte] $pixel.B); $writer.Write([byte] $pixel.G); $writer.Write([byte] $pixel.R); $writer.Write([byte] $pixel.A)
            if ($pixel.A -eq 0) {
                $at = ($size - 1 - $row) * $maskStride + [Math]::Floor($x / 8)
                $mask[$at] = $mask[$at] -bor (0x80 -shr ($x % 8))
            }
        }
    }
    $writer.Write($mask)
    $writer.Flush()
    return , $stream.ToArray()
}

$images = @()
foreach ($size in $sizes) {
    $bitmap = Draw $size
    if ($Previews -ne '') {
        New-Item -ItemType Directory -Force $Previews | Out-Null
        $bitmap.Save((Join-Path $Previews "icon-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $images += , @{ Size = $size; Bytes = (IconImage $bitmap) }
    $bitmap.Dispose()
}

$file = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([int16] 0)                 # ICONDIR
$writer.Write([int16] 1)
$writer.Write([int16] $images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $side = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte] $side)
    $writer.Write([byte] $side)
    $writer.Write([byte] 0)
    $writer.Write([byte] 0)
    $writer.Write([int16] 1)
    $writer.Write([int16] 32)
    $writer.Write([int] $image.Bytes.Length)
    $writer.Write([int] $offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write([byte[]] $image.Bytes) }
$writer.Flush()

[System.IO.File]::WriteAllBytes($icoFile, $file.ToArray())
[System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath($favicon), $file.ToArray())
"wrote $icoFile and $([System.IO.Path]::GetFullPath($favicon)) ($($file.Length) bytes, sizes $($sizes -join ', '))"
