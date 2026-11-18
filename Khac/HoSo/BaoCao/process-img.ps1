# Xu ly anh: auto-crop khoang trang duoi + slice trang chu + nen JPG
Add-Type -AssemblyName System.Drawing
$src = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\img2'
$dst = 'C:\Users\ASUS\Downloads\SmartPort\BaoCao\img-doc'

# --- Ham auto-crop khoang trang duoi cung ---
function AutoCropBottom([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $bg = $bmp.GetPixel(5, $h - 3)   # mau nen goc duoi
    $lastContent = $h - 1
    for ($y = $h - 1; $y -ge 0; $y--) {
        $diff = $false
        for ($x = 0; $x -lt $w; $x += 7) {
            $p = $bmp.GetPixel($x, $y)
            if ([math]::Abs($p.R - $bg.R) -gt 12 -or [math]::Abs($p.G - $bg.G) -gt 12 -or [math]::Abs($p.B - $bg.B) -gt 12) { $diff = $true; break }
        }
        if ($diff) { $lastContent = $y; break }
    }
    $newH = [math]::Min($h, $lastContent + 40)
    if ($newH -ge $h) { return $bmp }
    $crop = New-Object System.Drawing.Bitmap($w, $newH)
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0,0,$w,$newH)), (New-Object System.Drawing.Rectangle(0,0,$w,$newH)), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    return $crop
}

# --- Encoder JPG ---
$enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$ep = New-Object System.Drawing.Imaging.EncoderParameters(1)
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality, [long]88)
function SaveJpg([System.Drawing.Bitmap]$bmp, $path) {
    $nw = 1100; $nh = [int]($bmp.Height * $nw / $bmp.Width)
    $b = New-Object System.Drawing.Bitmap($nw, $nh)
    $g = [System.Drawing.Graphics]::FromImage($b); $g.InterpolationMode = 'HighQualityBicubic'
    $g.DrawImage($bmp, 0, 0, $nw, $nh); $g.Dispose()
    $b.Save($path, $enc, $ep); $b.Dispose()
}

# --- Slice trang chu ---
$homeSrc = "$src\01-home-full.png"
$homeImg = [System.Drawing.Image]::FromFile($homeSrc)
$homeBmp = New-Object System.Drawing.Bitmap($homeImg)
$homeImg.Dispose()
$homeC = AutoCropBottom $homeBmp
$hw = $homeC.Width; $hh = $homeC.Height
$slice = 1780; $n = [math]::Ceiling($hh / $slice)
for ($i = 0; $i -lt $n; $i++) {
    $y = $i * $slice; $sh = [math]::Min($slice, $hh - $y)
    $part = New-Object System.Drawing.Bitmap($hw, $sh)
    $g = [System.Drawing.Graphics]::FromImage($part)
    $g.DrawImage($homeC, (New-Object System.Drawing.Rectangle(0,0,$hw,$sh)), (New-Object System.Drawing.Rectangle(0,$y,$hw,$sh)), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    SaveJpg $part "$dst\01-home-p$($i+1).jpg"
    $part.Dispose()
}
$homeC.Dispose(); $homeBmp.Dispose()
"Trang chu: cat $n lat (cao $hh px sau crop)"

# --- Cac anh con lai: auto-crop + JPG ---
$cnt = 0
Get-ChildItem $src -Filter *.png | Where-Object { $_.Name -ne '01-home-full.png' } | ForEach-Object {
    $im = [System.Drawing.Image]::FromFile($_.FullName)
    $bmp = New-Object System.Drawing.Bitmap($im); $im.Dispose()
    $c = AutoCropBottom $bmp
    SaveJpg $c "$dst\$($_.BaseName).jpg"
    if ($c -ne $bmp) { $c.Dispose() }
    $bmp.Dispose(); $cnt++
}
"Da xu ly $cnt anh khac -> img-doc"
(Get-ChildItem $dst -Filter *.jpg | Measure-Object).Count.ToString() + ' JPG trong img-doc'