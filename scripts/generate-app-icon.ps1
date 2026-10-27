<#
.SYNOPSIS
    Generates a valid, multi-resolution Windows .ico file from assets/logo.png
    compatible with Windows Explorer, WPF, and Inno Setup (ISCC).
#>
[CmdletBinding()]
param(
    [string]$SourcePngPath = "assets/logo.png",
    [string[]]$DestinationPaths = @("assets/app.ico", "src/burroQueue.Presentation/Assets/app.ico"),
    [int[]]$Sizes = @(256, 128, 64, 48, 32, 24, 16)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $SourcePngPath)) {
    Write-Error "Source PNG file not found: $SourcePngPath"
    exit 1
}

$src = [System.Drawing.Image]::FromFile((Resolve-Path $SourcePngPath).Path)
$frames = [System.Collections.Generic.List[byte[]]]::new()
$entries = [System.Collections.Generic.List[PSObject]]::new()

Write-Host "Processing icon frames from: $SourcePngPath ($($src.Width)x$($src.Height))" -ForegroundColor Cyan

foreach ($size in $Sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Scale maintaining aspect ratio, centered in a square canvas
    $scale = [Math]::Min($size / $src.Width, $size / $src.Height)
    $drawW = [int][Math]::Round($src.Width * $scale)
    $drawH = [int][Math]::Round($src.Height * $scale)
    $drawX = [int][Math]::Round(($size - $drawW) / 2)
    $drawY = [int][Math]::Round(($size - $drawH) / 2)

    $g.DrawImage($src, $drawX, $drawY, $drawW, $drawH)
    $g.Dispose()

    # Build BMP DIB data (BITMAPINFOHEADER + XOR pixel data + 1-bpp AND mask)
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms

    $andRowBytes = [int]([Math]::Ceiling($size / 32.0)) * 4
    $xorSize = $size * $size * 4
    $andSize = $andRowBytes * $size

    # BITMAPINFOHEADER (40 bytes)
    $bw.Write([uint32]40)                      # biSize
    $bw.Write([int32]$size)                    # biWidth
    $bw.Write([int32]($size * 2))              # biHeight (XOR height + AND height)
    $bw.Write([uint16]1)                       # biPlanes
    $bw.Write([uint16]32)                      # biBitCount
    $bw.Write([uint32]0)                       # biCompression = BI_RGB (uncompressed)
    $bw.Write([uint32]($xorSize + $andSize))   # biSizeImage
    $bw.Write([int32]0)                        # biXPelsPerMeter
    $bw.Write([int32]0)                        # biYPelsPerMeter
    $bw.Write([uint32]0)                       # biClrUsed
    $bw.Write([uint32]0)                       # biClrImportant

    # Lock bits for fast pixel read
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixelBytes = New-Object byte[] ($size * $size * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixelBytes, 0, $pixelBytes.Length)
    $bmp.UnlockBits($data)

    # XOR bitmap image (stored bottom-to-top in standard DIB format)
    for ($y = $size - 1; $y -ge 0; $y--) {
        $rowOffset = $y * $size * 4
        $bw.Write($pixelBytes, $rowOffset, $size * 4)
    }

    # AND mask (1 bit per pixel, bottom-to-top, padded to 32-bit DWORD boundaries)
    $andMask = New-Object byte[] $andSize
    for ($y = $size - 1; $y -ge 0; $y--) {
        $rowIndexFromBottom = ($size - 1 - $y)
        $maskRowOffset = $rowIndexFromBottom * $andRowBytes
        for ($x = 0; $x -lt $size; $x++) {
            $pixelAlpha = $pixelBytes[($y * $size + $x) * 4 + 3]
            if ($pixelAlpha -lt 128) {
                $byteInRow = [int]($x -shr 3)
                $bitInByte = 7 - ($x % 8)
                $andMask[$maskRowOffset + $byteInRow] = $andMask[$maskRowOffset + $byteInRow] -bor (1 -shl $bitInByte)
            }
        }
    }
    $bw.Write($andMask, 0, $andMask.Length)

    $frameBytes = $ms.ToArray()
    $bw.Dispose()
    $ms.Dispose()
    $bmp.Dispose()

    $frames.Add($frameBytes)
    $entries.Add([PSCustomObject]@{
        Width = if ($size -ge 256) { [byte]0 } else { [byte]$size }
        Height = if ($size -ge 256) { [byte]0 } else { [byte]$size }
        Colors = [byte]0
        Reserved = [byte]0
        Planes = [uint16]1
        BitCount = [uint16]32
        BytesInRes = [uint32]$frameBytes.Length
    })

    Write-Host "  -> Frame ${size}x${size} generated ($($frameBytes.Length) bytes)" -ForegroundColor Gray
}

$src.Dispose()

# Build the complete .ico in memory
$icoStream = New-Object System.IO.MemoryStream
$icoWriter = New-Object System.IO.BinaryWriter $icoStream

# ICONDIR header (6 bytes)
$icoWriter.Write([uint16]0)                # Reserved
$icoWriter.Write([uint16]1)                # Type = 1 (ICO)
$icoWriter.Write([uint16]$entries.Count)   # Count

$dataOffset = 6 + ($entries.Count * 16)
for ($i = 0; $i -lt $entries.Count; $i++) {
    $e = $entries[$i]
    $icoWriter.Write([byte]$e.Width)
    $icoWriter.Write([byte]$e.Height)
    $icoWriter.Write([byte]$e.Colors)
    $icoWriter.Write([byte]$e.Reserved)
    $icoWriter.Write([uint16]$e.Planes)
    $icoWriter.Write([uint16]$e.BitCount)
    $icoWriter.Write([uint32]$e.BytesInRes)
    $icoWriter.Write([uint32]$dataOffset)
    $dataOffset += $e.BytesInRes
}

foreach ($fb in $frames) {
    $icoWriter.Write($fb, 0, $fb.Length)
}

$finalBytes = $icoStream.ToArray()
$icoWriter.Dispose()
$icoStream.Dispose()

foreach ($dest in $DestinationPaths) {
    $parent = Split-Path -Parent $dest
    if ($parent -and -not (Test-Path $parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [System.IO.File]::WriteAllBytes($dest, $finalBytes)
    Write-Host "[OK] Wrote valid icon to: $dest ($($finalBytes.Length) bytes)" -ForegroundColor Green
}
