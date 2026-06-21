#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Stops a running Codex Workflow Recorder by creating a stop signal file.
.PARAMETER Name
    Recording name to stop. If omitted, stops the most recent one.
#>

param([string]$Name)

$pluginRoot = Split-Path -Parent $PSScriptRoot

if ($Name) {
    $recDir = Join-Path (Join-Path (Join-Path $pluginRoot "assets") "recordings") $Name
} else {
    $recRoot = Join-Path (Join-Path $pluginRoot "assets") "recordings"
    $recDir = Get-ChildItem -Path $recRoot -Directory | 
        Where-Object { Test-Path (Join-Path $_.FullName "recorder.pid") } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 |
        ForEach-Object { $_.FullName }
}

if (-not $recDir -or -not (Test-Path $recDir)) {
    Write-Host "No active recording found." -ForegroundColor Yellow
    exit 1
}

# Create stop signal file
$signalFile = Join-Path $recDir "stop.signal"
Write-Host "Creating stop signal..." -ForegroundColor Yellow
Set-Content -Path $signalFile -Value "stop" -NoNewline

# Wait for process to exit gracefully
$pidFile = Join-Path $recDir "recorder.pid"
$procId = $null
if (Test-Path $pidFile) {
    $procId = (Get-Content $pidFile -Raw).Trim()
}

if ($procId) {
    Write-Host "Waiting for recorder (PID: $procId) to stop..." -ForegroundColor Yellow
    $waitCount = 0
    do {
        Start-Sleep -Milliseconds 500
        $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
        $waitCount++
    } while ($proc -and $waitCount -lt 10)

    if ($proc) {
        Write-Host "Force killing..." -ForegroundColor Red
        $proc.Kill()
    }
}

# Check what was captured
$eventFile = Join-Path $recDir "events.jsonl"
$eventCount = 0
if (Test-Path $eventFile) {
    $eventCount = (Get-Content $eventFile | Measure-Object).Count
}

$ssDir = Join-Path $recDir "screenshots"
$screenshotCount = 0
if (Test-Path $ssDir) {
    $screenshotCount = (Get-ChildItem $ssDir -Filter "*.png" | Measure-Object).Count
}

$nameFile = Join-Path $recDir "recording-name.txt"
$recordingName = "unnamed"
if (Test-Path $nameFile) {
    $recordingName = (Get-Content $nameFile -Raw).Trim()
}

Write-Host "`n✓ Recording stopped: $recordingName" -ForegroundColor Green
Write-Host "  Events: $eventCount"
Write-Host "  Screenshots: $screenshotCount"
Write-Host "  Location: $recDir"
