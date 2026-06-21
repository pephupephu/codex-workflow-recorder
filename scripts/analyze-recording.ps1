#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes a recorded session and generates a reusable Codex skill.
.PARAMETER Name
    Recording name to analyze. If omitted, shows available recordings.
.PARAMETER SkillName
    Name for the generated skill. Default: derived from recording name.
.PARAMETER Description
    Description of the workflow goal. If omitted, auto-generated from events.
#>

param(
    [string]$Name,
    [string]$SkillName,
    [string]$Description
)

$pluginRoot = Split-Path -Parent $PSScriptRoot
$recRoot = Join-Path (Join-Path $pluginRoot "assets") "recordings"

# List recordings if no name given
if (-not $Name) {
    Write-Host "=== Available Recordings ===" -ForegroundColor Cyan
    Get-ChildItem -Path $recRoot -Directory | ForEach-Object {
        $dir = $_.FullName
        $eventFile = Join-Path $dir "events.jsonl"
        $nameFile = Join-Path $dir "recording-name.txt"
        $ssDir = Join-Path $dir "screenshots"
        
        $rName = "unnamed"
        if (Test-Path $nameFile) { $rName = (Get-Content $nameFile -Raw).Trim() }
        
        $eventCount = 0
        if (Test-Path $eventFile) { $eventCount = (Get-Content $eventFile | Measure-Object).Count }
        
        $ssCount = 0
        if (Test-Path $ssDir) { $ssCount = (Get-ChildItem $ssDir -Filter "*.png" | Measure-Object).Count }
        
        Write-Host "  $($_.Name)" -ForegroundColor Green
        Write-Host "    Name:     $rName"
        Write-Host "    Events:   $eventCount"
        Write-Host "    Screens:  $ssCount"
        Write-Host "    Path:     $dir"
        Write-Host ""
    }
    return
}

$recDir = Join-Path $recRoot $Name
if (-not (Test-Path $recDir)) {
    Write-Host "Recording '$Name' not found at: $recDir" -ForegroundColor Red
    exit 1
}

$eventFile = Join-Path $recDir "events.jsonl"
if (-not (Test-Path $eventFile)) {
    Write-Host "No events found in recording '$Name'." -ForegroundColor Red
    exit 1
}

$screenshotDir = Join-Path $recDir "screenshots"

Write-Host "=== Analyzing Recording: $Name ===" -ForegroundColor Cyan

# Read events
$lines = Get-Content $eventFile
$events = @()
$lines | ForEach-Object { $events += ($_ | ConvertFrom-Json) }
$actionEvents = $events | Where-Object { $_.type -ne "recording_started" -and $_.type -ne "recording_stopped" }

Write-Host "Total events: $($events.Count)"
Write-Host "Action events: $($actionEvents.Count)"

# Extract workflow pattern
$windowChanges = $actionEvents | Where-Object { $_.data.window -ne "" } | ForEach-Object { $_.data.window } | Select-Object -Unique
$clickCount = ($actionEvents | Where-Object { $_.data.action -eq "click" } | Measure-Object).Count
$keypressCount = ($actionEvents | Where-Object { $_.data.action -eq "keypress" } | Measure-Object).Count

Write-Host "Windows/Apps used:" -ForegroundColor Yellow
$windowChanges | ForEach-Object { Write-Host "  - $_" }
Write-Host "Clicks: $clickCount"
Write-Host "Keypresses: $keypressCount"

# Build the skill name
if (-not $SkillName) {
    $SkillName = $Name -replace "^recording-\d{8}-", "workflow-"
    if ($SkillName -eq "workflow-") { $SkillName = "workflow-$Name" }
}

# Generate into a clean output structure
$generatedDir = Join-Path (Join-Path $pluginRoot "skills") "generated"
$skillDir = Join-Path $generatedDir $SkillName
New-Item -ItemType Directory -Path $skillDir -Force | Out-Null
$assetsDir = Join-Path $skillDir "assets"
New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
$ssTargetDir = Join-Path $assetsDir "screenshots"
New-Item -ItemType Directory -Path $ssTargetDir -Force | Out-Null

# Generate SKILL.md
$sb = New-Object System.Text.StringBuilder

# Frontmatter
[void]$sb.AppendLine("---")
[void]$sb.AppendLine("name: $SkillName")
[void]$sb.AppendLine("description: >")
if ($Description) {
    $Description -split "\. " | ForEach-Object {
        [void]$sb.AppendLine("  $_.")
    }
} else {
    $descLine = "  Recorded workflow. "
    $descLine += "Uses: " + ($windowChanges -join ", ") + ". "
    [void]$sb.AppendLine($descLine)
}
[void]$sb.AppendLine("---")
[void]$sb.AppendLine("")

# Convert kebab-name to Title Case name
$titleSegments = $SkillName -split "-"
$titleSegments = $titleSegments | ForEach-Object {
    $s = $_
    if ($s.Length -gt 0) {
        $first = $s[0].ToString().ToUpper()
        $rest = $s.Substring(1).ToLower()
        $first + $rest
    } else { "" }
}
$titleName = $titleSegments -join " "
[void]$sb.AppendLine("# $titleName")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Steps (Recorded on Windows)")
[void]$sb.AppendLine("")

$stepNum = 1
$actionEvents | ForEach-Object {
    $e = $_.data
    [void]$sb.AppendLine("### Step $stepNum")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- **Action**: $($e.action)")
    [void]$sb.AppendLine("- **Detail**: $($e.detail)")
    if ($e.window) { [void]$sb.AppendLine("- **Window**: $($e.window)") }
    if ($e.x -and $e.x -ne 0) { [void]$sb.AppendLine("- **Position**: ($($e.x), $($e.y))") }
    if ($e.screenshot) { [void]$sb.AppendLine("- **Screenshot**: screenshots/$($e.screenshot)") }
    [void]$sb.AppendLine("- **Time**: $($e.time)")
    [void]$sb.AppendLine("")
    $stepNum++
}

$skillMd = $sb.ToString()
$skillMdPath = Join-Path $skillDir "SKILL.md"
[System.IO.File]::WriteAllText($skillMdPath, $skillMd, [System.Text.Encoding]::UTF8)

# Copy screenshots
if (Test-Path $screenshotDir) {
    Get-ChildItem $screenshotDir -Filter "*.png" | ForEach-Object {
        Copy-Item $_.FullName $ssTargetDir -Force
    }
}

# Write workflow summary
$summary = @{
    source = "codex-workflow-recorder-windows"
    recorded_at = (Get-Date -Format "o")
    workflow_name = $SkillName
    total_steps = $actionEvents.Count
    windows_used = @($windowChanges)
    clicks = $clickCount
    keypresses = $keypressCount
    generated_at = (Get-Date -Format "o")
}
$summaryJson = ConvertTo-Json -InputObject $summary -Depth 5
$summaryPath = Join-Path $assetsDir "workflow-summary.json"
Out-File -InputObject $summaryJson -FilePath $summaryPath -Encoding utf8

Write-Host "`n✓ Skill generated!" -ForegroundColor Green
Write-Host "  Skill name: $SkillName"
Write-Host "  Skill path: $skillMdPath"
Write-Host "  Steps recorded: $stepNum"
Write-Host ""
Write-Host "To install, copy to Codex skills:" -ForegroundColor Cyan
Write-Host "  Copy-Item '$skillDir' -Destination '`$env:USERPROFILE\.codex\skills\$SkillName' -Recurse"
Write-Host ""
Write-Host "Then in a new thread, say: Use $SkillName to..." -ForegroundColor Cyan
