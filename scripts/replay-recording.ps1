#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Replays a recorded workflow by simulating mouse/keyboard events.
.PARAMETER Name
    Name of the recording to replay.
.PARAMETER Speed
    Speed multiplier (default: 1, higher = faster).
.PARAMETER DryRun
    Preview events without executing.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Name,
    [int]$Speed = 1,
    [switch]$DryRun
)

$pluginRoot = Split-Path -Parent $PSScriptRoot
$csc = "C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\csc.exe"

$recDir = Join-Path (Join-Path (Join-Path $pluginRoot "assets") "recordings") $Name
$eventsFile = Join-Path $recDir "events.jsonl"

if (-not (Test-Path $recDir)) {
    Write-Host "Recording not found: $Name" -ForegroundColor Red
    Write-Host "Available recordings:" -ForegroundColor Yellow
    $parent = Split-Path $recDir -Parent
    if (Test-Path $parent) {
        Get-ChildItem $parent -Directory | Select-Object Name
    }
    exit 1
}

if (-not (Test-Path $eventsFile)) {
    Write-Host "Events file not found: $eventsFile" -ForegroundColor Red
    Write-Host "This recording may not have events data for replay." -ForegroundColor Yellow
    exit 1
}

Write-Host "=== Codex Workflow Replayer ===" -ForegroundColor Cyan
Write-Host "Recording: $Name" -ForegroundColor White
Write-Host "Directory: $recDir" -ForegroundColor White
Write-Host "Speed: ${Speed}x" -ForegroundColor White
if ($DryRun) { Write-Host "Mode: Dry-run (preview only)" -ForegroundColor Yellow }

# Compile replayer
$csFile = Join-Path $PSScriptRoot "Replayer.cs"
$exeFile = Join-Path $recDir "replayer.exe"

Write-Host "`nCompiling replayer..." -ForegroundColor Yellow
& $csc -target:exe -platform:anycpu `
    -reference:System.Windows.Forms.dll `
    -reference:System.Drawing.dll `
    -out:"$exeFile" "$csFile" 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0 -and (Test-Path $exeFile)) {
    Write-Host "✓ Compiled: $((Get-Item $exeFile).Length) bytes" -ForegroundColor Green
    
    # Build arguments
    $args = @("`"$recDir`"")
    if ($Speed -ne 1) { $args += "--speed"; $args += "$Speed" }
    if ($DryRun) { $args += "--dry-run" }
    
    Write-Host "`nStarting replay..." -ForegroundColor Yellow
    Write-Host "WARNING: Do not use mouse/keyboard during replay!" -ForegroundColor Red
    Write-Host ""
    
    if (-not $DryRun) {
        # Give user time to prepare
        for ($i = 3; $i -ge 1; $i--) {
            Write-Host "Starting in $i..." -ForegroundColor Green
            Start-Sleep 1
        }
    }
    
    # Run replayer
    & $exeFile $args
    
    Write-Host "`nReplay complete." -ForegroundColor Cyan
    Write-Host "Screenshots saved to: $(Join-Path $recDir 'replay-screenshots')" -ForegroundColor White
} else {
    Write-Host "✗ Compilation failed" -ForegroundColor Red
    # Show compiler errors
    & $csc -target:exe -platform:anycpu `
        -reference:System.Windows.Forms.dll `
        -reference:System.Drawing.dll `
        -out:"$exeFile" "$csFile" 2>&1
}