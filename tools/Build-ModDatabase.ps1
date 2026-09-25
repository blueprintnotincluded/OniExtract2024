<#
.SYNOPSIS
Merges every mods\<dir>\buildings.json into mods\mod_database.json.

.DESCRIPTION
Reads mods\manifest.json for the list of supported mods, validates each per-mod
buildings.json (required fields, unique building names), stamps in the per-mod DLL
state recorded by tools\Refresh-ModSources.ps1, and writes the combined
mods\mod_database.json used by the website for modded buildings.
#>

$repoRoot = Split-Path -Parent $PSScriptRoot
$modsRoot = Join-Path $repoRoot 'mods'
$manifest = Get-Content (Join-Path $modsRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json

$requiredFields = @('name', 'nameString', 'widthInCells', 'heightInCells', 'materialCategory',
    'materialMass', 'buildLocationRule', 'permittedRotations', 'viewMode', 'utilities',
    'planCategory', 'tech', 'kanim')

$allBuildings = @()
$modMeta = @()
$seenNames = @{}
$errors = 0

foreach ($mod in $manifest.mods) {
    $modDir = Join-Path $modsRoot $mod.dir
    $buildingsPath = Join-Path $modDir 'buildings.json'
    $statePath = Join-Path $modDir 'source-state.json'

    if (-not (Test-Path $buildingsPath)) {
        Write-Warning "$($mod.dir): buildings.json missing -- mod skipped"
        $errors++
        continue
    }
    $data = Get-Content $buildingsPath -Raw -Encoding UTF8 | ConvertFrom-Json

    $state = $null
    if (Test-Path $statePath) {
        $state = Get-Content $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    } else {
        Write-Warning "$($mod.dir): source-state.json missing -- run tools\Refresh-ModSources.ps1 to record the DLL state"
    }

    foreach ($b in $data.buildings) {
        foreach ($f in $requiredFields) {
            if (-not ($b.PSObject.Properties.Name -contains $f)) {
                Write-Warning "$($mod.dir): building '$($b.name)' missing required field '$f'"
                $errors++
            }
        }
        if ($seenNames.ContainsKey($b.name)) {
            Write-Warning "duplicate building name '$($b.name)' in $($mod.dir) (also in $($seenNames[$b.name]))"
            $errors++
        } else {
            $seenNames[$b.name] = $mod.dir
        }
        # Stamp the owning mod onto each entry so the merged list is self-describing.
        if (-not ($b.PSObject.Properties.Name -contains 'mod')) {
            $b | Add-Member -NotePropertyName 'mod' -NotePropertyValue $mod.workshopId
        } else {
            $b.mod = $mod.workshopId
        }
        $allBuildings += $b
    }

    $modMeta += [ordered]@{
        workshopId    = $mod.workshopId
        title         = $mod.title
        dir           = $mod.dir
        dll           = $mod.dll
        dllSha256     = if ($state) { $state.dllSha256 } else { $null }
        dllDate       = if ($state) { $state.dllDate } else { $null }
        buildingCount = @($data.buildings).Count
        buildings     = @($data.buildings | ForEach-Object { $_.name })
    }
}

$db = [ordered]@{
    ExportFileName = 'mod_database'
    generatedAt    = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    schema         = 'see mods/README.md -- building.json bBuildingDefList subset + mod extensions'
    mods           = $modMeta
    bBuildingDefList = $allBuildings
}

$outPath = Join-Path $modsRoot 'mod_database.json'
$db | ConvertTo-Json -Depth 15 | Out-File $outPath -Encoding utf8

Write-Host "Wrote $outPath -- $(@($allBuildings).Count) buildings from $(@($modMeta).Count) mods."
if ($errors -gt 0) {
    Write-Warning "$errors validation warning(s) above -- review before shipping."
    exit 1
}
