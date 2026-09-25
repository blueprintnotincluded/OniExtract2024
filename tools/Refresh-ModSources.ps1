<#
.SYNOPSIS
Detects updates to supported mods and refreshes the decompiled source snapshots.

.DESCRIPTION
For each mod in mods\manifest.json:
  - Computes the installed DLL's SHA256 and compares it to mods\<dir>\source-state.json.
  - UNCHANGED  -> extraction data is still valid, nothing to do.
  - CHANGED / NEW -> re-decompiles the tracked types into mods\<dir>\decompiled\ and
    updates source-state.json. Review the diff (git diff mods\<dir>\decompiled) and follow
    the "How to update" checklist in that mod's NOTES.md, then run tools\Build-ModDatabase.ps1.

Requires the ilspycmd dotnet global tool (see GAME_INTERNALS.md).

.PARAMETER Force
Re-decompile every mod even if the DLL hash is unchanged.
#>
param(
    [switch]$Force
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$modsRoot = Join-Path $repoRoot 'mods'
$manifest = Get-Content (Join-Path $modsRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$steamRoot = $manifest._meta.steamModsRoot

$anyChanged = $false

foreach ($mod in $manifest.mods) {
    $label = "$($mod.title) ($($mod.workshopId))"
    $dllPath = Join-Path (Join-Path $steamRoot $mod.workshopId) $mod.dll
    $modDir = Join-Path $modsRoot $mod.dir
    $statePath = Join-Path $modDir 'source-state.json'

    if (-not (Test-Path $dllPath)) {
        Write-Warning "$label -- DLL not found at $dllPath (mod uninstalled?). Skipping; existing extraction data kept."
        continue
    }

    $hash = (Get-FileHash $dllPath -Algorithm SHA256).Hash
    $dllDate = (Get-Item $dllPath).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')

    $state = $null
    if (Test-Path $statePath) {
        $state = Get-Content $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    }

    if ($state -and $state.dllSha256 -eq $hash -and -not $Force) {
        Write-Host "UNCHANGED  $label"
        continue
    }

    $status = if ($state) { 'CHANGED' } else { 'NEW' }
    Write-Host "$status  $label -- decompiling $($mod.trackedTypes.Count) tracked types..."

    $decompDir = Join-Path $modDir 'decompiled'
    New-Item -ItemType Directory -Force $decompDir | Out-Null

    foreach ($typeName in $mod.trackedTypes) {
        $shortName = ($typeName -split '\.')[-1]
        $outFile = Join-Path $decompDir "$shortName.cs"
        # ilspycmd writes the decompiled type to stdout; its version-nag goes to stderr.
        $source = & ilspycmd $dllPath -t $typeName
        if ($LASTEXITCODE -ne 0 -or -not $source) {
            Write-Warning "  failed to decompile $typeName (type renamed/removed? update manifest.json trackedTypes)"
            continue
        }
        $source | Out-File $outFile -Encoding utf8
        Write-Host "  -> decompiled\$shortName.cs"
    }

    $newState = [ordered]@{
        workshopId  = $mod.workshopId
        title       = $mod.title
        dll         = $mod.dll
        dllSha256   = $hash
        dllDate     = $dllDate
        refreshedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    $newState | ConvertTo-Json | Out-File $statePath -Encoding utf8

    if ($status -eq 'CHANGED') {
        $anyChanged = $true
        Write-Host "  REVIEW: git diff -- `"mods/$($mod.dir)/decompiled`"  then update buildings.json per NOTES.md" -ForegroundColor Yellow
    }
}

if ($anyChanged) {
    Write-Host "`nOne or more mods changed. After updating the affected buildings.json files, run tools\Build-ModDatabase.ps1." -ForegroundColor Yellow
} else {
    Write-Host "`nAll extraction data is current."
}
