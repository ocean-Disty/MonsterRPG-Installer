# ===========================================================================
#  make-icon.ps1 - turn MonsterRPGIcon.png into MonsterRPG.ico
#
#  Windows will not let a program carry a .png as its icon; it has to be a
#  .ico, which is a container holding the same picture at several sizes. This
#  builds that container.
#
#  You only need to run this if the picture changes. build.bat runs it for you
#  when the .ico is missing or older than the .png.
#
#  Usage:  powershell -ExecutionPolicy Bypass -File make-icon.ps1 <in.png> <out.ico>
# ===========================================================================

param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Small sizes are stored the old way (an uncompressed bitmap) because a few
# corners of Windows - Alt+Tab, the classic title bar - still read icons with
# code that predates PNG support. Large sizes are stored as PNG, because an
# uncompressed 256x256 is a quarter of a megabyte each and nothing that old
# ever asks for one.
$bmpSizes = @(16, 20, 24, 32, 40, 48, 64)
$pngSizes = @(128, 256)

function Resize-Bitmap {
    param([System.Drawing.Image]$Image, [int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.CompositingMode    = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $attr = New-Object System.Drawing.Imaging.ImageAttributes
        $attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        $rect = New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)
        $g.DrawImage($Image, $rect, 0, 0, $Image.Width, $Image.Height, [System.Drawing.GraphicsUnit]::Pixel, $attr)
        $attr.Dispose()
    }
    finally { $g.Dispose() }
    return $bmp
}

# A DIB inside a .ico: BITMAPINFOHEADER, then the pixels bottom-up, then a
# 1-bit transparency mask. The mask is left all-zero (meaning "opaque") because
# the 32-bit pixels carry their own alpha; the header still has to say the
# image is twice as tall as it really is, which is how the format has always
# marked "there is a mask after this".
function ConvertTo-DibBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height

    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                             [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($w * $h * 4)
    try {
        for ($y = 0; $y -lt $h; $y++) {
            $srcRow = [IntPtr]::Add($data.Scan0, $y * $data.Stride)
            # bottom-up
            [System.Runtime.InteropServices.Marshal]::Copy($srcRow, $pixels, ($h - 1 - $y) * $w * 4, $w * 4)
        }
    }
    finally { $Bitmap.UnlockBits($data) }

    $maskRowBytes = [math]::Floor(($w + 31) / 32) * 4
    $maskBytes = $maskRowBytes * $h

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    try {
        $bw.Write([uint32]40)              # biSize
        $bw.Write([int32]$w)               # biWidth
        $bw.Write([int32]($h * 2))         # biHeight - image plus mask
        $bw.Write([uint16]1)               # biPlanes
        $bw.Write([uint16]32)              # biBitCount
        $bw.Write([uint32]0)               # biCompression - BI_RGB
        $bw.Write([uint32]($pixels.Length + $maskBytes))
        $bw.Write([int32]0); $bw.Write([int32]0)   # pixels per metre
        $bw.Write([uint32]0); $bw.Write([uint32]0) # palette
        $bw.Write($pixels)
        $bw.Write((New-Object byte[] $maskBytes))
        $bw.Flush()
        return $ms.ToArray()
    }
    finally { $bw.Dispose(); $ms.Dispose() }
}

function ConvertTo-PngBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $ms = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        return $ms.ToArray()
    }
    finally { $ms.Dispose() }
}

$source = (Resolve-Path -LiteralPath $Source).Path
$image = [System.Drawing.Image]::FromFile($source)

$entries = New-Object System.Collections.ArrayList
try {
    # The [byte[]] casts are not decoration. A PowerShell function hands back an
    # array as a loose object list, and BinaryWriter then picks its
    # single-byte overload and writes one byte instead of the whole image - a
    # .ico with a perfect table of contents and no pictures in it.
    foreach ($size in $bmpSizes) {
        $bmp = Resize-Bitmap -Image $image -Size $size
        try { [void]$entries.Add(@{ Size = $size; Bytes = [byte[]](ConvertTo-DibBytes -Bitmap $bmp) }) }
        finally { $bmp.Dispose() }
    }
    foreach ($size in $pngSizes) {
        $bmp = Resize-Bitmap -Image $image -Size $size
        try { [void]$entries.Add(@{ Size = $size; Bytes = [byte[]](ConvertTo-PngBytes -Bitmap $bmp) }) }
        finally { $bmp.Dispose() }
    }
}
finally { $image.Dispose() }

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
try {
    $w.Write([uint16]0)                  # reserved
    $w.Write([uint16]1)                  # 1 = icon
    $w.Write([uint16]$entries.Count)

    $offset = 6 + (16 * $entries.Count)
    foreach ($e in $entries) {
        # 256 is written as 0; the field is a single byte and 256 does not fit.
        $dim = if ($e.Size -ge 256) { 0 } else { $e.Size }
        $w.Write([byte]$dim)             # width
        $w.Write([byte]$dim)             # height
        $w.Write([byte]0)                # colours in palette
        $w.Write([byte]0)                # reserved
        $w.Write([uint16]1)              # planes
        $w.Write([uint16]32)             # bits per pixel
        $w.Write([uint32]$e.Bytes.Length)
        $w.Write([uint32]$offset)
        $offset += $e.Bytes.Length
    }
    foreach ($e in $entries) { $w.Write($e.Bytes) }
    $w.Flush()

    $dir = Split-Path -Parent $Destination
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [System.IO.File]::WriteAllBytes($Destination, $out.ToArray())
}
finally { $w.Dispose(); $out.Dispose() }

Write-Host ("  wrote {0} ({1} sizes, {2} bytes)" -f $Destination, $entries.Count, (Get-Item -LiteralPath $Destination).Length)
