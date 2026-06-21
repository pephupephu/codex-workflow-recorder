#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Compiles and starts the Codex Workflow Recorder (global hooks + screenshots).
.PARAMETER Name
    Name for this recording session. Default: auto-generated timestamp name.
#>

param([string]$Name)

$pluginRoot = Split-Path -Parent $PSScriptRoot
$csc = "C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if (-not $Name) {
    $Name = "recording-" + (Get-Date -Format "yyyyMMdd-HHmmss")
}

$recDir = Join-Path (Join-Path (Join-Path $pluginRoot "assets") "recordings") $Name
New-Item -ItemType Directory -Path $recDir -Force | Out-Null
$ssDir = Join-Path $recDir "screenshots"
New-Item -ItemType Directory -Path $ssDir -Force | Out-Null

$csFile = Join-Path $PSScriptRoot "Recorder.cs"
$exeFile = Join-Path $recDir "recorder.exe"

Write-Host "=== Codex Workflow Recorder ===" -ForegroundColor Cyan
Write-Host "Compiling recorder..." -ForegroundColor Yellow

& $csc -target:winexe -platform:anycpu `
    -reference:System.Windows.Forms.dll `
    -reference:System.Drawing.dll `
    -out:"$exeFile" "$csFile" 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0 -and (Test-Path $exeFile)) {
    Write-Host "✓ Compiled: $((Get-Item $exeFile).Length) bytes" -ForegroundColor Green
    Write-Host "Starting recorder (hidden)..." -ForegroundColor Yellow
    
    $proc = Start-Process -FilePath $exeFile -ArgumentList "`"$recDir`"","`"$Name`"" -WindowStyle Hidden -PassThru
    
    Start-Sleep -Milliseconds 500
    
    if (-not $proc.HasExited) {
        Write-Host "`n✓ RECORDING ACTIVE" -ForegroundColor Green
        Write-Host "  Name:     $Name"
        Write-Host "  Output:   $recDir"
        Write-Host "  PID:      $($proc.Id)"
        Write-Host "`n  Perform your workflow now." -ForegroundColor Cyan
        Write-Host "  To stop: Press Ctrl+Shift+F12 or run:" -ForegroundColor Cyan
        Write-Host "  stop-recording.ps1 -Name $Name"
        
        return @{ Name = $Name; OutputDir = $recDir; Pid = $proc.Id }
    } else {
        Write-Host "✗ Recorder exited unexpectedly" -ForegroundColor Red
    }
} else {
    Write-Host "✗ Compilation failed" -ForegroundColor Red
}
