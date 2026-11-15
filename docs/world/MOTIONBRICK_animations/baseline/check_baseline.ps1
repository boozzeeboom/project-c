# T-MB01B: verify that the animation baseline has not been modified.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\check_baseline.ps1
#   powershell -ExecutionPolicy Bypass -File .\check_baseline.ps1 -Root "C:\Projects\ProjectC_client"
#
# Exit code: 0 = all files match the baseline; 1 = changed/missing files.
# This script is read-only; it does not touch Unity assets.

param(
    [string]$Root = "C:\Projects\ProjectC_client",
    [string]$Manifest = (Join-Path $PSScriptRoot "baseline_hashes.tsv"),
    [switch]$SkipPackages
)

if (-not (Test-Path $Manifest)) {
    Write-Error "Manifest not found: $Manifest"
    exit 1
}

$failures = 0
$checked = 0

Get-Content $Manifest | ForEach-Object {
    $line = $_.Trim()
    if ([string]::IsNullOrWhiteSpace($line)) { return }

    $parts = $line -split "`t", 2
    if ($parts.Count -ne 2) {
        Write-Host "BAD LINE: $line" -ForegroundColor Yellow
        $failures++
        return
    }

    $expected = $parts[0].Trim().ToLower()
    $relative = $parts[1].Trim()

    if ($SkipPackages -and $relative -like 'Packages/*') {
        Write-Host "SKIP     $relative (package guard disabled)" -ForegroundColor DarkGray
        return
    }

    $path = Join-Path $Root ($relative -replace '/', '\')
    $checked++

    if (-not (Test-Path $path)) {
        Write-Host "MISSING  $relative" -ForegroundColor Red
        $failures++
        return
    }

    $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLower()
    if ($actual -ne $expected) {
        Write-Host "CHANGED  $relative" -ForegroundColor Red
        Write-Host "         expected $expected"
        Write-Host "         actual   $actual"
        $failures++
    } else {
        Write-Host "OK       $relative" -ForegroundColor Green
    }
}

Write-Host ""
if ($failures -eq 0) {
    Write-Host "BASELINE OK: $checked files match." -ForegroundColor Green
    exit 0
}

Write-Host "BASELINE FAILED: $failures of $checked files changed/missing." -ForegroundColor Red
exit 1
