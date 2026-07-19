<#
.SYNOPSIS
Extracts the build-menu icon PNG for every mod building in mods\mod_database.json.

.DESCRIPTION
For each building in each mods\<dir>\buildings.json:
  - Resolves its kanim (e.g. "airlock_door_kanim") to the mod's anim\assets\<folder>
    (folder = kanim name minus "_kanim", matched case-insensitively).
  - Parses <base>_build.bytes (Klei "BILD" format, same layout as tools\Parse-KanimBuild.ps1).
  - Crops the "ui" symbol's frame 0 out of the texture atlas.
  - Writes mods\images\<buildingName>.png  (filename == building `name`, matching the
    website contract ui_image/<prefabId>.png — see WEBSITE_POSTPROCESSING.md).

These are footprint-style flat icons (the same kind the website's legacy stretch-to-footprint
path expects), so no uiImageRect is needed. Connection-state sprites for drag-build utilities
are NOT produced here — see the "Art" section of mods\README.md.
#>

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$modsRoot = Join-Path $repoRoot 'mods'
$manifest = Get-Content (Join-Path $modsRoot 'manifest.json') -Raw | ConvertFrom-Json
$steamRoot = $manifest._meta.steamModsRoot
$outDir = Join-Path $modsRoot 'images'
New-Item -ItemType Directory -Force $outDir | Out-Null

function Read-KleiString($reader) {
    $len = $reader.ReadInt32()
    if ($len -le 0) { return '' }
    return [System.Text.Encoding]::UTF8.GetString($reader.ReadBytes($len))
}

# Parses a BILD file; returns @{ symbols = <name -> frame list with atlas px rects>; }
function Parse-Bild([string]$buildFile, [int]$atlasW, [int]$atlasH) {
    $bytes = [System.IO.File]::ReadAllBytes($buildFile)
    $ms = New-Object System.IO.MemoryStream(,$bytes)
    $r = New-Object System.IO.BinaryReader($ms)
    try {
        $magic = [System.Text.Encoding]::ASCII.GetString($r.ReadBytes(4))
        if ($magic -ne 'BILD') { throw "Not a BILD file: $buildFile" }
        $version = $r.ReadInt32()
        $numSymbols = $r.ReadInt32()
        $null = $r.ReadInt32()   # total frame count
        $null = Read-KleiString $r  # build name

        $rawSymbols = @()
        for ($s = 0; $s -lt $numSymbols; $s++) {
            $hash = $r.ReadInt32()
            if ($version -gt 9) { $null = $r.ReadInt32() }  # path
            $null = $r.ReadInt32()  # color
            $null = $r.ReadInt32()  # flags
            $frameCount = $r.ReadInt32()
            $frames = @()
            for ($f = 0; $f -lt $frameCount; $f++) {
                $null = $r.ReadInt32()  # sourceFrameNum
                $null = $r.ReadInt32()  # duration
                $null = $r.ReadInt32()  # buildImageIdx
                $null = $r.ReadSingle(); $null = $r.ReadSingle()  # pivotX/Y
                $null = $r.ReadSingle(); $null = $r.ReadSingle()  # pivotW/H
                $u1 = $r.ReadSingle(); $v1 = $r.ReadSingle()
                $u2 = $r.ReadSingle(); $v2 = $r.ReadSingle()
                # UVs are TOP-LEFT origin (v grows downward) — verified against pixel
                # content 2026-07-19; the earlier "(1 - v2)" bottom-left formula was wrong.
                $frames += [pscustomobject]@{
                    pxX = [math]::Round($u1 * $atlasW)
                    pxY = [math]::Round($v1 * $atlasH)
                    pxW = [math]::Round(($u2 - $u1) * $atlasW)
                    pxH = [math]::Round(($v2 - $v1) * $atlasH)
                }
            }
            $rawSymbols += [pscustomobject]@{ hash = $hash; frames = $frames }
        }

        $numHashes = $r.ReadInt32()
        $hashNames = @{}
        for ($h = 0; $h -lt $numHashes; $h++) {
            $hv = $r.ReadInt32()
            $hashNames[$hv] = Read-KleiString $r
        }

        $symbols = @{}
        foreach ($sym in $rawSymbols) {
            $symbols[[string]$hashNames[$sym.hash]] = $sym.frames
        }
        return $symbols
    } finally {
        $r.Dispose(); $ms.Dispose()
    }
}

$ok = 0; $failed = 0

foreach ($mod in $manifest.mods) {
    $data = Get-Content (Join-Path (Join-Path $modsRoot $mod.dir) 'buildings.json') -Raw | ConvertFrom-Json
    $assetsRoot = Join-Path (Join-Path $steamRoot $mod.workshopId) 'anim\assets'

    foreach ($b in $data.buildings) {
        $kanimBase = $b.kanim -replace '_kanim$', ''
        $folder = Get-ChildItem $assetsRoot -Directory | Where-Object { $_.Name -ieq $kanimBase } | Select-Object -First 1
        if (-not $folder) {
            Write-Warning "$($b.name): no anim folder matching '$kanimBase' under $assetsRoot"
            $failed++; continue
        }

        $base = $folder.Name
        $buildFile = Join-Path $folder.FullName "${base}_build.bytes"
        $pngFile = Join-Path $folder.FullName "$base.png"
        if (-not (Test-Path $buildFile)) { $buildFile = (Get-ChildItem $folder.FullName -Filter *_build.bytes | Select-Object -First 1).FullName }
        if (-not (Test-Path $pngFile)) { $pngFile = (Get-ChildItem $folder.FullName -Filter *.png | Select-Object -First 1).FullName }

        $atlas = [System.Drawing.Bitmap]::FromFile($pngFile)
        try {
            $symbols = Parse-Bild $buildFile $atlas.Width $atlas.Height
            if (-not $symbols.ContainsKey('ui')) {
                Write-Warning "$($b.name): kanim '$base' has no 'ui' symbol (has: $($symbols.Keys -join ', ')) -- pick a fallback symbol manually"
                $failed++; continue
            }
            $fr = $symbols['ui'][0]
            $rect = New-Object System.Drawing.Rectangle($fr.pxX, $fr.pxY, $fr.pxW, $fr.pxH)
            $crop = $atlas.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $outFile = Join-Path $outDir "$($b.name).png"
                $crop.Save($outFile, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Host "OK  $($b.name).png  ($($fr.pxW)x$($fr.pxH) from $base)"
                $ok++
            } finally { $crop.Dispose() }
        } finally {
            $atlas.Dispose()
        }
    }
}

Write-Host "`n$ok icon(s) written to $outDir, $failed failure(s)."
if ($failed -gt 0) { exit 1 }
