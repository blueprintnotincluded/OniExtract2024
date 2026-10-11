<#
.SYNOPSIS
    Downloads classdata.tpk, the Unity type-tree database AssetDump needs, next to this script.

.DESCRIPTION
    Shipped ONI builds strip the type trees from their asset files, so a reader has to be told
    each class's field layout. AssetRipper publishes that database from CI only: the releases
    of AssetRipper/Tpk carry no files. This takes the newest successful "Type Tree Tpk" run on
    master and keeps its uncompressed artifact. Rerun it after a Unity upgrade of the game.

    Requires the GitHub CLI (gh), signed in.
#>
$ErrorActionPreference = 'Stop'

$runId = gh run list --repo AssetRipper/Tpk --branch master --workflow 'Type Tree Tpk' `
    --status success --limit 1 --json databaseId --jq '.[0].databaseId'
if (-not $runId) { throw 'No successful Type Tree Tpk run found on AssetRipper/Tpk.' }

$staging = Join-Path ([IO.Path]::GetTempPath()) "assetdump-tpk-$runId"
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
gh run download $runId --repo AssetRipper/Tpk --name uncompressed_file --dir $staging
if ($LASTEXITCODE -ne 0) { throw "gh run download failed for run $runId." }

$tpk = Get-ChildItem $staging -Filter *.tpk -Recurse | Select-Object -First 1
if (-not $tpk) { throw "Run $runId had no .tpk in its uncompressed_file artifact." }

$target = Join-Path $PSScriptRoot 'classdata.tpk'
Copy-Item $tpk.FullName $target -Force
Remove-Item -Recurse -Force $staging
Write-Host "classdata.tpk from AssetRipper/Tpk run $runId -> $target"
