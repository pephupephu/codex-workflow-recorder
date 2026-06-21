# Codex Workflow Recorder & Replayer (Windows)

Record mouse clicks and keyboard actions, then replay them automatically on Windows.

## Features

- **Record**: Capture mouse clicks and keyboard inputs with screenshots
- **Replay**: Automatically simulate recorded mouse/keyboard actions
- **Merge**: Combine multiple recordings into end-to-end workflows
- **Customizable**: Override parameters during replay (speed, etc.)

## Installation

Install as a Codex plugin from the personal marketplace, or clone this repo to your plugins directory.

## Usage

### Record a Workflow

```powershell
# Start recording
.\scripts\compile-and-start.ps1 -Name "my-workflow"

# Perform your actions...
# Press Ctrl+Shift+F12 to stop recording

# List all recordings
.\scripts\list-recordings.ps1
```

### Replay a Recorded Workflow

```powershell
# Preview events (dry run)
.\scripts\replay-recording.ps1 -Name "my-workflow" -DryRun

# Replay at normal speed
.\scripts\replay-recording.ps1 -Name "my-workflow"

# Replay at 2x speed
.\scripts\replay-recording.ps1 -Name "my-workflow" -Speed 2
```

**⚠️ WARNING**: During replay, do not use mouse or keyboard as the replayer will control both!

### Merge Recordings

Multiple recordings can be merged into a single end-to-end workflow skill using the Codex CLI.

## Recording Data

Each recording is stored in `assets/recordings/<name>/`:
- `events.jsonl` - Raw event data (mouse clicks, key presses)
- `screenshots/` - Screenshots at each step
- `workflow-recording.json` - Recording summary
- `replay-screenshots/` - Screenshots taken during replay

## Requirements

- Windows OS
- .NET Framework 4.0+
- PowerShell 5.0+