param([string]$Root = (Get-Location).Path, [switch]$RefreshBackgrounds)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$manifest = Get-Content -LiteralPath (Join-Path $Root 'Docs/Art/guild_assets_manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$sources = (Get-Content -LiteralPath (Join-Path $Root 'Docs/Art/guild_asset_sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json).sources
foreach ($source in $sources) {
    $asset = $manifest.assets | Where-Object { $_.name -eq $source.name } | Select-Object -First 1
    if (!$asset -or !(Test-Path -LiteralPath $source.path)) { throw "Missing manifest/source: $($source.name)" }
    $directory = Join-Path $Root ("Assets/Resources/Art/" + $asset.folder)
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $target = Join-Path $directory ($asset.name + '.png')
    if (!(Test-Path -LiteralPath $target) -or ($RefreshBackgrounds -and !$asset.transparent)) {
        $inputImage = [System.Drawing.Bitmap]::FromFile($source.path)
        $output = New-Object System.Drawing.Bitmap([int]$asset.width,[int]$asset.height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($output)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)
        if (!$asset.transparent) {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
            $graphics.Clear([System.Drawing.Color]::FromArgb(255,23,24,31))
        }
        $graphics.DrawImage($inputImage,0,0,[int]$asset.width,[int]$asset.height)
        $output.Save($target,[System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose(); $output.Dispose(); $inputImage.Dispose()
        Write-Output ("Exported " + $asset.name)
    }
    $metaPath = $target + '.meta'
    if (!(Test-Path -LiteralPath $metaPath)) {
        $border = [int]$asset.border
        $guid = [guid]::NewGuid().ToString('N')
        $meta = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureType: 8
  textureShape: 1
  spriteMode: 1
  spriteMeshType: 0
  spritePixelsToUnits: 100
  spritePivot: {x: 0.5, y: 0.5}
  spriteBorder: {x: $border, y: $border, z: $border, w: $border}
  alphaIsTransparency: 1
  alphaUsage: 1
  npotScale: 0
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  textureCompression: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    textureFormat: -1
    textureCompression: 0
    overridden: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"@
        [IO.File]::WriteAllText($metaPath,$meta + [Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
    }
}

