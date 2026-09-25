param([Parameter(Mandatory=$true)][string]$BuildFile, [string]$PngFile)

$bytes = [System.IO.File]::ReadAllBytes($BuildFile)
$ms = New-Object System.IO.MemoryStream(,$bytes)
$r = New-Object System.IO.BinaryReader($ms)

function Read-KleiString($reader) {
    $len = $reader.ReadInt32()
    if ($len -le 0) { return "" }
    return [System.Text.Encoding]::UTF8.GetString($reader.ReadBytes($len))
}

$magic = [System.Text.Encoding]::ASCII.GetString($r.ReadBytes(4))
if ($magic -ne 'BILD') { throw "Not a BILD file: $magic" }
$version = $r.ReadInt32()
$numSymbols = $r.ReadInt32()
$numFrames = $r.ReadInt32()
$name = Read-KleiString $r

# PNG dimensions from IHDR (big-endian at offset 16)
$imgW = 0; $imgH = 0
if ($PngFile -and (Test-Path $PngFile)) {
    $png = [System.IO.File]::ReadAllBytes($PngFile)
    $imgW = ([int]$png[16] -shl 24) -bor ([int]$png[17] -shl 16) -bor ([int]$png[18] -shl 8) -bor [int]$png[19]
    $imgH = ([int]$png[20] -shl 24) -bor ([int]$png[21] -shl 16) -bor ([int]$png[22] -shl 8) -bor [int]$png[23]
}

$symbols = @()
for ($s = 0; $s -lt $numSymbols; $s++) {
    $hash = $r.ReadInt32()
    $path = if ($version -gt 9) { $r.ReadInt32() } else { $hash }
    $color = $r.ReadInt32()
    $flags = $r.ReadInt32()
    $frameCount = $r.ReadInt32()
    $frames = @()
    for ($f = 0; $f -lt $frameCount; $f++) {
        $fr = [ordered]@{
            sourceFrameNum = $r.ReadInt32()
            duration       = $r.ReadInt32()
            buildImageIdx  = $r.ReadInt32()
            pivotX = $r.ReadSingle(); pivotY = $r.ReadSingle()
            pivotW = $r.ReadSingle(); pivotH = $r.ReadSingle()
            u1 = $r.ReadSingle(); v1 = $r.ReadSingle()
            u2 = $r.ReadSingle(); v2 = $r.ReadSingle()
        }
        if ($imgW -gt 0) {
            # UVs are TOP-LEFT origin, same as PNG pixels (verified against pixel content
            # 2026-07-19 — an earlier version wrongly flipped with (1 - v2))
            $fr.pxX = [math]::Round($fr.u1 * $imgW)
            $fr.pxY = [math]::Round($fr.v1 * $imgH)
            $fr.pxW = [math]::Round(($fr.u2 - $fr.u1) * $imgW)
            $fr.pxH = [math]::Round(($fr.v2 - $fr.v1) * $imgH)
        }
        $frames += [pscustomobject]$fr
    }
    $symbols += [pscustomobject]@{ hash=$hash; path=$path; flags=$flags; frameCount=$frameCount; frames=$frames }
}

# Trailing hash table maps int hashes -> original string names
$numHashes = $r.ReadInt32()
$hashNames = @{}
for ($h = 0; $h -lt $numHashes; $h++) {
    $hv = $r.ReadInt32()
    $hashNames[$hv] = Read-KleiString $r
}

"file: $BuildFile"
"build name: $name  (version $version, $numSymbols symbols, $numFrames frames, atlas ${imgW}x${imgH})"
""
foreach ($sym in $symbols) {
    $symName = $hashNames[$sym.hash]
    "symbol '$symName' (flags=$($sym.flags), $($sym.frameCount) frames)"
    foreach ($fr in $sym.frames) {
        $px = if ($imgW -gt 0) { "  atlas rect x=$($fr.pxX) y=$($fr.pxY) w=$($fr.pxW) h=$($fr.pxH)" } else { "" }
        "  frame $($fr.sourceFrameNum): pivot=($([math]::Round($fr.pivotX,3)), $([math]::Round($fr.pivotY,3))) size=($([math]::Round($fr.pivotW,1)) x $([math]::Round($fr.pivotH,1)))$px"
    }
}
