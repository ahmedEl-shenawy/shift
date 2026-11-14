param([string]$DayPath, [string]$NightPath, [string]$OutDir, [double]$XTop = 0.6, [double]$XBot = 0.4)
Add-Type -AssemblyName System.Drawing

$day = [System.Drawing.Image]::FromFile($DayPath)
$night = [System.Drawing.Image]::FromFile($NightPath)
$W = $day.Width; $H = $day.Height

$img = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($img)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.InterpolationMode = 'HighQualityBicubic'
$g.CompositingQuality = 'HighQuality'

# diagonal seam: from top (xTop) to bottom (xBot)
$xTop = [int]($W * $XTop); $xBot = [int]($W * $XBot)
function P($x, $y) { New-Object System.Drawing.PointF $x, $y }
$leftPoly  = [System.Drawing.PointF[]]@((P 0 0), (P $xTop 0), (P $xBot $H), (P 0 $H))
$rightPoly = [System.Drawing.PointF[]]@((P $xTop 0), (P $W 0), (P $W $H), (P $xBot $H))
$leftPath = New-Object System.Drawing.Drawing2D.GraphicsPath; $leftPath.AddPolygon($leftPoly)
$rightPath = New-Object System.Drawing.Drawing2D.GraphicsPath; $rightPath.AddPolygon($rightPoly)

$g.DrawImage($night, 0, 0, $W, $H)
$g.SetClip($leftPath)
$g.DrawImage($day, 0, 0, $W, $H)
$g.ResetClip()

# glowing seam
foreach ($pair in @(@(30, 14), @(16, 34), @(8, 90), @(3, 255))) {
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($pair[1], 255, 248, 225)), $pair[0]
    $g.DrawLine($pen, $xTop, 0, $xBot, $H)
    $pen.Dispose()
}

# top + bottom vignettes for text legibility
$topBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, $W, 300), ([System.Drawing.Color]::FromArgb(150, 8, 6, 24)), ([System.Drawing.Color]::FromArgb(0, 8, 6, 24)), 90
$g.FillRectangle($topBrush, 0, 0, $W, 300)
$botBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, ($H - 260), $W, 260), ([System.Drawing.Color]::FromArgb(0, 8, 6, 24)), ([System.Drawing.Color]::FromArgb(185, 8, 6, 24)), 90
$g.FillRectangle($botBrush, 0, ($H - 260), $W, 260)

# title "S H I F T" as a path, so we can outline it and fill each side differently
$family = New-Object System.Drawing.FontFamily "Segoe UI Black"
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = 'Center'
$title = New-Object System.Drawing.Drawing2D.GraphicsPath
$letters = "SHIFT".ToCharArray()
$size = 196; $spacing = 178
$startX = $W / 2 - ($letters.Length - 1) * $spacing / 2
for ($i = 0; $i -lt $letters.Length; $i++) {
    $title.AddString([string]$letters[$i], $family, 0, $size, (P ($startX + $i * $spacing) 18), $fmt)
}

# shadow
$shadow = $title.Clone()
$m = New-Object System.Drawing.Drawing2D.Matrix; $m.Translate(0, 10); $shadow.Transform($m)
$g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(120, 0, 0, 10))), $shadow)

$outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 18, 14, 40)), 14
$outline.LineJoin = 'Round'
$g.DrawPath($outline, $title)

$dayFill = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 40, $W, 220), ([System.Drawing.Color]::FromArgb(255, 255, 236, 170)), ([System.Drawing.Color]::FromArgb(255, 255, 150, 50)), 90
$nightFill = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 40, $W, 220), ([System.Drawing.Color]::FromArgb(255, 200, 240, 255)), ([System.Drawing.Color]::FromArgb(255, 80, 150, 255)), 90
$g.SetClip($leftPath);  $g.FillPath($dayFill, $title)
$g.SetClip($rightPath); $g.FillPath($nightFill, $title)
$g.ResetClip()

# tagline
$tagFont = New-Object System.Drawing.Font "Bahnschrift SemiBold", 44, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$tagRect = New-Object System.Drawing.RectangleF 0, ($H - 150), $W, 60
$g.DrawString("Day and night are one key apart.", $tagFont, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(160, 0, 0, 0))), (New-Object System.Drawing.RectangleF 3, ($H - 147), $W, 60), $fmt)
$g.DrawString("Day and night are one key apart.", $tagFont, [System.Drawing.Brushes]::White, $tagRect, $fmt)

# keycap hint: [ SHIFT ]
$capFont = New-Object System.Drawing.Font "Bahnschrift SemiBold", 26, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$capW = 150; $capH = 46; $capX = $W / 2 - $capW / 2; $capY = $H - 80
$cap = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 10
$cap.AddArc($capX, $capY, $r * 2, $r * 2, 180, 90)
$cap.AddArc($capX + $capW - $r * 2, $capY, $r * 2, $r * 2, 270, 90)
$cap.AddArc($capX + $capW - $r * 2, $capY + $capH - $r * 2, $r * 2, $r * 2, 0, 90)
$cap.AddArc($capX, $capY + $capH - $r * 2, $r * 2, $r * 2, 90, 90)
$cap.CloseFigure()
$g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 245, 242, 235))), $cap)
$g.DrawPath((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 120, 115, 140)), 3), $cap)
$capFmt = New-Object System.Drawing.StringFormat; $capFmt.Alignment = 'Center'; $capFmt.LineAlignment = 'Center'
$g.DrawString("SHIFT", $capFont, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 26, 50))), (New-Object System.Drawing.RectangleF $capX, ($capY + 1), $capW, $capH), $capFmt)

$g.Dispose()

New-Item -ItemType Directory -Force $OutDir | Out-Null
$full = Join-Path $OutDir "cover_1260x1000.png"
$img.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)

$small = New-Object System.Drawing.Bitmap 630, 500
$gs = [System.Drawing.Graphics]::FromImage($small)
$gs.InterpolationMode = 'HighQualityBicubic'; $gs.SmoothingMode = 'AntiAlias'; $gs.PixelOffsetMode = 'HighQuality'
$gs.DrawImage($img, 0, 0, 630, 500)
$gs.Dispose()
$smallPath = Join-Path $OutDir "itch_cover_630x500.png"
$small.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Png)

$img.Dispose(); $small.Dispose(); $day.Dispose(); $night.Dispose()
"$full`n$smallPath"
