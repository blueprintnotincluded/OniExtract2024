<#
.SYNOPSIS
Fallback-merges the offline mod extraction into a game export folder — additive only.

.DESCRIPTION
The PRIMARY way to get mod buildings into the export is the normal in-game export with
the mods enabled: every export pass (main-menu JSON, building-image sweep, connection
sprites) iterates Assets.BuildingDefs, which includes every loaded mod's buildings, at
full image quality. This script is the OFFLINE FALLBACK for buildings that path cannot
cover (mods incompatible with the Extract mod, or buildings the in-game sweep misses).

For each building in mods\mod_database.json NOT already present in database\building.json:
  1. Appends it to bBuildingDefList (with its `mod` = workshopId marker).
  2. Registers it in buildingAndSubcategoryDataPairs under its planCategory
     (honoring addAfter ordering within the category).
  3. Copies mods\images\<prefabId>.png into ui_image\ — only if no icon exists there
     (never overwrites an in-game hi-res render with the low-res offline crop).
  4. Stamps a root `modMergeInfo` field recording what was appended and what was
     skipped as natively exported.

Buildings the game exported natively are left untouched. Idempotent: previously
offline-merged entries (marker: the `mod` property) and their menu pairs are stripped
before re-evaluating, so re-running after a fresh game export or a mod-data update
always converges. The stripped building.json is saved alongside as
building.pre-mod-merge.json each run.

.PARAMETER ExportDir
The export folder the game writes and the website ingests.
Default: %USERPROFILE%\Documents\Klei\OxygenNotIncluded\export
#>
param(
    [string]$ExportDir = "$env:USERPROFILE\Documents\Klei\OxygenNotIncluded\export"
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$modsRoot = Join-Path $repoRoot 'mods'
$buildingJsonPath = Join-Path $ExportDir 'database\building.json'
$uiImageDir = Join-Path $ExportDir 'ui_image'
$imagesDir = Join-Path $modsRoot 'images'

if (-not (Test-Path $buildingJsonPath)) { throw "No building.json at $buildingJsonPath -- wrong -ExportDir?" }
if (-not (Test-Path $uiImageDir)) { throw "No ui_image folder at $uiImageDir -- wrong -ExportDir?" }

$modDb = Get-Content (Join-Path $modsRoot 'mod_database.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$doc = Get-Content $buildingJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

# --- 1. strip only previously OFFLINE-merged entries ------------------------------
# Markers: `offlineMerged` (stamped by this script on append) or `configClass` (an
# offline-schema-only field, catches merges made before the marker existed). The in-game
# exporter now emits `mod`/`modTitle` on natively exported mod buildings too, so `mod`
# alone must NOT be treated as an offline marker — native entries are never touched.
$existing = @($doc.bBuildingDefList)
$stripNames = @($existing | Where-Object {
        $_.PSObject.Properties.Name -contains 'offlineMerged' -or
        $_.PSObject.Properties.Name -contains 'configClass'
    } | ForEach-Object { $_.name }) | Sort-Object -Unique

$clean = @($existing | Where-Object { $stripNames -notcontains $_.name })
foreach ($catProp in $doc.buildingAndSubcategoryDataPairs.PSObject.Properties) {
    $catProp.Value = @($catProp.Value | Where-Object { $stripNames -notcontains $_.Key })
}
$vanillaCount = $clean.Count

# Keep an as-exported (offline-merge-free) copy in the export ROOT — not database\,
# so it doesn't ride along when the export/database folder is pulled to the website.
$doc.bBuildingDefList = $clean
[System.IO.File]::WriteAllText((Join-Path $ExportDir 'building.pre-mod-merge.json'), ($doc | ConvertTo-Json -Depth 24))
$staleSnapshot = Join-Path $ExportDir 'database\building.pre-mod-merge.json'
if (Test-Path $staleSnapshot) { Remove-Item $staleSnapshot }

# --- 2. append only buildings the in-game export did NOT cover -------------------
$nativeNames = @($clean | ForEach-Object { $_.name })
$toAppend = @($modDb.bBuildingDefList | Where-Object { $nativeNames -notcontains $_.name })
$skippedNative = @($modDb.bBuildingDefList | Where-Object { $nativeNames -contains $_.name } | ForEach-Object { $_.name })
foreach ($n in $skippedNative) { Write-Host "native: $n already in building.json (in-game export) -- offline data skipped" }
foreach ($b in $toAppend) {
    if ($b.PSObject.Properties.Name -contains 'offlineMerged') { $b.offlineMerged = $true }
    else { $b | Add-Member -NotePropertyName 'offlineMerged' -NotePropertyValue $true }
    # Keep the mod/modTitle field pair uniform with natively exported entries.
    if (-not ($b.PSObject.Properties.Name -contains 'modTitle')) {
        $meta = $modDb.mods | Where-Object { $_.workshopId -eq $b.mod } | Select-Object -First 1
        if ($meta) { $b | Add-Member -NotePropertyName 'modTitle' -NotePropertyValue $meta.title }
    }
}

$doc.bBuildingDefList = $clean + $toAppend

$errors = 0
foreach ($b in $toAppend) {
    $catKey = $doc.buildingAndSubcategoryDataPairs.PSObject.Properties.Name |
        Where-Object { $_ -ieq $b.planCategory } | Select-Object -First 1
    if (-not $catKey) {
        Write-Warning "$($b.name): plan category '$($b.planCategory)' not found in export -- building appended but not in build menu"
        $errors++
        continue
    }
    $pairs = [System.Collections.ArrayList]@($doc.buildingAndSubcategoryDataPairs.$catKey)
    $newPair = [pscustomobject]@{ Key = $b.name; Value = $b.planSubCategory }
    $inserted = $false
    if ($b.addAfter) {
        for ($i = 0; $i -lt $pairs.Count; $i++) {
            if ($pairs[$i].Key -eq $b.addAfter) {
                $pairs.Insert($i + 1, $newPair)
                $inserted = $true
                break
            }
        }
    }
    if (-not $inserted) { $null = $pairs.Add($newPair) }
    $doc.buildingAndSubcategoryDataPairs.$catKey = @($pairs)
    Write-Host "menu: $catKey/$($b.planSubCategory) += $($b.name)$(if ($inserted) { " (after $($b.addAfter))" })"
}

# --- 3. provenance marker --------------------------------------------------------
$mergeInfo = [pscustomobject]@{
    mergedAt        = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    source          = 'OniExtract2024 mods/mod_database.json (offline fallback merge; see mods/README.md)'
    appended        = @($toAppend | ForEach-Object { $_.name })
    nativelyExported = $skippedNative
    mods            = $modDb.mods
}
if ($doc.PSObject.Properties.Name -contains 'modMergeInfo') {
    $doc.modMergeInfo = $mergeInfo
} else {
    $doc | Add-Member -NotePropertyName 'modMergeInfo' -NotePropertyValue $mergeInfo
}

[System.IO.File]::WriteAllText($buildingJsonPath, ($doc | ConvertTo-Json -Depth 24))

# --- 4. icons: fill gaps only — an existing PNG is an in-game hi-res render ------
$iconCount = 0
foreach ($b in $modDb.bBuildingDefList) {
    $dst = Join-Path $uiImageDir "$($b.name).png"
    if (Test-Path $dst) { continue }
    $src = Join-Path $imagesDir "$($b.name).png"
    if (Test-Path $src) {
        Copy-Item $src $dst
        $iconCount++
        Write-Host "icon: $($b.name).png filled from offline crop (no in-game render present)"
    } else {
        Write-Warning "$($b.name): no icon in ui_image\ and none at $src (run tools\Export-ModImages.ps1) -- website import will fail validation for this building"
        $errors++
    }
}

$total = $vanillaCount + @($toAppend).Count
Write-Host "`nAppended $(@($toAppend).Count) offline building(s) ($vanillaCount as-exported -> $total total); $(@($skippedNative).Count) already exported in-game."
Write-Host "Filled $iconCount icon gap(s) in ui_image\."
Write-Host "As-exported snapshot: building.pre-mod-merge.json (export root)"
if ($errors -gt 0) {
    Write-Warning "$errors problem(s) above."
    exit 1
}
