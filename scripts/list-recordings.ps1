#!/usr/bin/env pwsh
<#
.SYNOPSIS
    List all recorded workflows with summary info.
.DESCRIPTION
    Scans the assets/recordings directory and displays a table of all recordings.
#>

$pluginRoot = Split-Path -Parent $PSScriptRoot
$recordingsDir = Join-Path $pluginRoot "assets" "recordings"

if (-not (Test-Path $recordingsDir)) {
    Write-Host "No recordings found yet." -ForegroundColor Yellow
    Write-Host "Start by using the 'record-workflow' skill to create one." -ForegroundColor Cyan
    exit 0
}

$workflows = Get-ChildItem -Path $recordingsDir -Directory

if ($workflows.Count -eq 0) {
    Write-Host "No recordings found yet." -ForegroundColor Yellow
    Write-Host "Start by using the 'record-workflow' skill to create one." -ForegroundColor Cyan
    exit 0
}

Write-Host "`n=== Recorded Workflows ===" -ForegroundColor Green
Write-Host ""

foreach ($wf in $workflows) {
    $stepsFile = Join-Path $wf.FullName "steps.json"
    if (Test-Path $stepsFile) {
        $steps = Get-Content $stepsFile -Raw | ConvertFrom-Json
        $stepCount = $steps.steps.Count
        $title = $steps.title
        $date = $steps.recorded_at
        Write-Host "  $($wf.Name)" -ForegroundColor Cyan
        Write-Host "    Title: $title"
        Write-Host "    Steps: $stepCount"
        Write-Host "    Recorded: $date"
        Write-Host ""
    }
}
