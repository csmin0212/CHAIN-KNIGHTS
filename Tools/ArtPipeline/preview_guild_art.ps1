param([string]$Root=(Get-Location).Path)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$manifest=Get-Content -LiteralPath (Join-Path $Root 'Docs/Art/guild_assets_manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$previews=Join-Path $Root 'Docs/Art/GuildPreviews'
New-Item -ItemType Directory -Path $previews -Force | Out-Null
foreach ($group in ($manifest.assets | Group-Object folder)) {
    $items=@($group.Group | Where-Object { Test-Path -LiteralPath (Join-Path $Root ("Assets/Resources/Art/"+$_.folder+"/"+$_.name+".png")) })
    if ($items.Count -eq 0) {continue}
    $columns=4; $cellWidth=260; $cellHeight=260
    if ($group.Name -eq 'Icons') {$columns=7; $cellWidth=160; $cellHeight=158}
    if ($group.Name -eq 'Backgrounds') {$columns=2; $cellWidth=520; $cellHeight=325}
    $rows=[int][Math]::Ceiling($items.Count/[double]$columns)
    $canvas=New-Object System.Drawing.Bitmap(($columns*$cellWidth),($rows*$cellHeight+44))
    $g=[System.Drawing.Graphics]::FromImage($canvas)
    $g.Clear([System.Drawing.Color]::FromArgb(255,23,24,31))
    $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $font=New-Object System.Drawing.Font('Arial',10)
    $titleFont=New-Object System.Drawing.Font('Arial',16)
    $g.DrawString(("CHAIN KNIGHTS / ADVENTURERS GUILD / "+$group.Name),$titleFont,[System.Drawing.Brushes]::Wheat,12,10)
    for($i=0;$i -lt $items.Count;$i++) {
        $item=$items[$i]
        $image=[System.Drawing.Bitmap]::FromFile((Join-Path $Root ("Assets/Resources/Art/"+$item.folder+"/"+$item.name+".png")))
        $x=($i%$columns)*$cellWidth; $y=[Math]::Floor($i/$columns)*$cellHeight+44
        $scale=[Math]::Min(($cellWidth-24)/[double]$image.Width,($cellHeight-44)/[double]$image.Height)
        $w=[int]($image.Width*$scale); $h=[int]($image.Height*$scale)
        $g.DrawImage($image,[int]($x+($cellWidth-$w)/2),[int]($y+($cellHeight-40-$h)/2),$w,$h)
        $g.DrawString(($item.name.Replace('icon_','').Replace('arthur_','')),$font,[System.Drawing.Brushes]::Wheat,[int]($x+8),[int]($y+$cellHeight-32))
        $image.Dispose()
    }
    $canvas.Save((Join-Path $previews ($group.Name.ToLowerInvariant()+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
    $font.Dispose();$titleFont.Dispose();$g.Dispose();$canvas.Dispose()
}

