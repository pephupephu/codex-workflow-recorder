---
name: record-workflow
description: >-
  Record a workflow by capturing mouse clicks and keyboard actions. 
  After recording, it can be replayed automatically via the Replay Engine.
---

# Record Workflow

Use this skill to record a desktop workflow (e.g., ComfyUI operations).

## How to Record

1. Run `compile-and-start.ps1` with a name for your recording
2. Perform your workflow steps (mouse clicks, keyboard inputs)
3. Press `Ctrl+Shift+F12` to stop recording
4. Find your recording in `assets/recordings/<name>/`

## How to Replay

1. Run `replay-recording.ps1 -Name <recording-name>`
2. Do NOT touch mouse or keyboard during replay
3. The replayer will simulate all recorded actions

## Tips

- Keep recordings short and focused on one task
- Use descriptive names for your recordings
- Multiple recordings can be merged into one skill
- Use `--speed 2` to replay at 2x speed