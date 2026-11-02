# Deterministic multi-resolution Windows application icon.
# Draws locally using built-in System.Drawing: no external images, secrets or network calls.
param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Color([int]$r,[int]$g,[int]$b) {
    return [System.Drawing.Color]::FromArgb(255,$r,$g,$b)
}
function Point([single]$x,[single]$y) {
    return [System.Drawing.PointF]::new($x,$y)
}
function RoundedRect([single]$x,[single]$y,[single]$w,[single]$h,[single]$radius) {
    $p=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter=2*$radius
    $p.AddArc($x,$y,$diameter,$diameter,180,90)
    $p.AddArc($x+$w-$diameter,$y,$diameter,$diameter,270,90)
    $p.AddArc($x+$w-$diameter,$y+$h-$diameter,$diameter,$diameter,0,90)
    $p.AddArc($x,$y+$h-$diameter,$diameter,$diameter,90,90)
    $p.CloseFigure()
    return $p
}

$sizes=@(16,24,32,48,64,128,256)
$images=[System.Collections.Generic.List[byte[]]]::new()
foreach($size in $sizes) {
    $bmp=[System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform([single]($size/128.0),[single]($size/128.0))

    $navy=[System.Drawing.SolidBrush]::new((Color 22 39 62))
    $gold=[System.Drawing.SolidBrush]::new((Color 245 192 73))
    $goldSide=[System.Drawing.SolidBrush]::new((Color 224 151 35))
    $goldLight=[System.Drawing.SolidBrush]::new((Color 255 216 128))
    $white=[System.Drawing.SolidBrush]::new((Color 248 250 253))
    $ink=[System.Drawing.SolidBrush]::new((Color 26 55 85))
    $frame=RoundedRect 3 3 122 122 24
    $g.FillPath($navy,$frame)

    # Package cube — high-contrast gold parcel silhouette, visible at 16px.
    $top=[System.Drawing.PointF[]]@(
        (Point 22 40),(Point 59 23),(Point 96 40),
        (Point 59 57))
    $left=[System.Drawing.PointF[]]@(
        (Point 22 40),(Point 59 57),(Point 59 103),(Point 22 85))
    $right=[System.Drawing.PointF[]]@(
        (Point 59 57),(Point 96 40),(Point 96 85),(Point 59 103))
    $g.FillPolygon($goldLight,$top)
    $g.FillPolygon($goldSide,$left)
    $g.FillPolygon($gold,$right)
    # Top carton sealing tape
    $tape=[System.Drawing.PointF[]]@(
        (Point 42 31),(Point 51 27),(Point 88 44),(Point 79 48))
    $g.FillPolygon($navy,$tape)

    # White printed label with small dark barcode, lower right face.
    $label=RoundedRect 67 62 38 31 4
    $g.FillPath($white,$label)
    $g.FillRectangle($ink,72,68,3,17)
    $g.FillRectangle($ink,78,68,2,17)
    $g.FillRectangle($ink,83,68,4,17)
    $g.FillRectangle($ink,91,68,2,17)
    $g.FillRectangle($ink,97,68,3,17)

    $stream=[System.IO.MemoryStream]::new()
    $bmp.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($stream.ToArray())
    $stream.Dispose()
    foreach($item in @($label,$frame,$navy,$gold,$goldSide,$goldLight,$white,$ink)){ $item.Dispose() }
    $g.Dispose();$bmp.Dispose()
}
$folder=[System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Output))
[System.IO.Directory]::CreateDirectory($folder) | Out-Null
$out=[System.IO.File]::Open($Output,[System.IO.FileMode]::Create,[System.IO.FileAccess]::Write)
try {
    $writer=[System.IO.BinaryWriter]::new($out)
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset=6+16*$sizes.Count
    for($i=0;$i -lt $sizes.Count;$i++){
        $s=$sizes[$i]
        $writer.Write([byte]$(if($s -eq 256){0}else{$s}))
        $writer.Write([byte]$(if($s -eq 256){0}else{$s}))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length)
        $writer.Write([uint32]$offset)
        $offset+=$images[$i].Length
    }
    foreach($bytes in $images){$writer.Write([byte[]]$bytes)}
    $writer.Flush()
    $writer.Dispose()
}
finally {
    $out.Dispose()
}
if(!(Test-Path $Output) -or (Get-Item $Output).Length -lt 1000) {
    throw "APP_ICON_GENERATION_FAILED"
}
Write-Host "Generated multi-resolution EXE icon: $Output"
