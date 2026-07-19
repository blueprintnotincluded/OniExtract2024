<#
.SYNOPSIS
Merges the offline mod extraction into a game export folder, as if the mods had been
part of the in-game export run.

.DESCRIPTION
Takes mods\mod_database.json and:
  1. Appends every mod building to database\building.json bBuildingDefList
     (entries keep their extension fields plus the `mod` = workshopId marker).
  2. Registers each building in buildingAndSubcategoryDataPairs under its planCategory
     (honoring addAfter ordering within the category).
  3. Copies mods\images\<prefabId>.png into ui_image\.
  4. Stamps a root `modMergeInfo` field (provenance: which mods, which DLL hashes, when).

Idempotent and re-export-safe: entries carrying a `mod` property (and their menu pairs)
are stripped before merging, so re-running after a fresh game export or a mod-data update
always converges. The stripped (vanilla-equivalent) building.json is saved alongside as
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

$modDb = Get-Content (Join-Path $modsRoot 'mod_database.json') -Raw | ConvertFrom-Json
$doc = Get-Content $buildingJsonPath -Raw | ConvertFrom-Json

# --- 1. strip any previously merged mod entries (idempotency) --------------------
$existing = @($doc.bBuildingDefList)
$stripNames = @($existing | Where-Object { $_.PSObject.Properties.Name -contains 'mod' } | ForEach-Object { $_.name })
$stripNames += @($modDb.bBuildingDefList | ForEach-Object { $_.name })
$stripNames = $stripNames | Sort-Object -Unique

$clean = @($existing | Where-Object { $stripNames -notcontains $_.name })
foreach ($catProp in $doc.buildingAndSubcategoryDataPairs.PSObject.Properties) {
    $catProp.Value = @($catProp.Value | Where-Object { $stripNames -notcontains $_.Key })
}
$vanillaCount = $clean.Count

# Keep a vanilla-equivalent copy beside the merged file.
$doc.bBuildingDefList = $clean
[System.IO.File]::WriteAllText((Join-Path $ExportDir 'database\building.pre-mod-merge.json'), ($doc | ConvertTo-Json -Depth 24))

# --- 2. append mod buildings + register menu pairs -------------------------------
$doc.bBuildingDefList = $clean + @($modDb.bBuildingDefList)

$errors = 0
foreach ($b in $modDb.bBuildingDefList) {
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
    mergedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    source   = 'OniExtract2024 mods/mod_database.json (offline extraction; see mods/README.md)'
    mods     = $modDb.mods
}
if ($doc.PSObject.Properties.Name -contains 'modMergeInfo') {
    $doc.modMergeInfo = $mergeInfo
} else {
    $doc | Add-Member -NotePropertyName 'modMergeInfo' -NotePropertyValue $mergeInfo
}

[System.IO.File]::WriteAllText($buildingJsonPath, ($doc | ConvertTo-Json -Depth 24))

# --- 4. icons --------------------------------------------------------------------
$iconCount = 0
foreach ($b in $modDb.bBuildingDefList) {
    $src = Join-Path $imagesDir "$($b.name).png"
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $uiImageDir "$($b.name).png") -Force
        $iconCount++
    } else {
        Write-Warning "$($b.name): no icon at $src (run tools\Export-ModImages.ps1) -- website import will fail validation for this building"
        $errors++
    }
}

$total = $vanillaCount + @($modDb.bBuildingDefList).Count
Write-Host "`nMerged $(@($modDb.bBuildingDefList).Count) mod buildings into building.json ($vanillaCount vanilla -> $total total)."
Write-Host "Copied $iconCount icon(s) into ui_image\."
Write-Host "Vanilla-equivalent snapshot: database\building.pre-mod-merge.json"
if ($errors -gt 0) {
    Write-Warning "$errors problem(s) above."
    exit 1
}
