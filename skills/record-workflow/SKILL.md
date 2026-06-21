---
name: record-workflow
description: Fully automatic workflow recording for Windows — equivalent to macOS Record & Replay. Use when Codex needs to capture a multi-step user workflow by silently recording mouse clicks, keyboard input, active window titles, and screenshots. Generates a reusable skill from the captured data. Best for: repetitive desktop tasks, workflows with specific preferences, processes easier to show than describe.
---

# Workflow Recording (Windows) — Automatic Mode

This skill implements the **full automatic workflow recording** experience on Windows,
equivalent to macOS Record & Replay. It uses a C# background recorder with global
mouse/keyboard hooks to silently observe the user's actions, then generates a reusable
Codex skill.

## How it works

1. **Start recording** — Tell Codex what workflow you want to record. A background
   recorder starts silently capturing mouse clicks, key presses, active window titles,
   and screenshots.

2. **Perform your workflow** — Use your computer normally. The recorder observes every
   action: what you click, what you type, which windows you switch between. Screenshots
   are taken automatically after each action.

3. **Stop recording** — Press **Ctrl+Shift+F12** or tell Codex to stop. The recorder
   saves everything to a structured recording.

4. **Analyze & generate skill** — Codex reads the events and screenshots, identifies
   the workflow pattern, and generates a reusable skill file.

5. **Use the skill** — In a new thread, ask Codex to use the generated skill. Codex
   replays the workflow using Computer Use, browser actions, shell commands, etc.

## Automatic Workflow

### Step 1: Prepare

Ask the user:

- What is the goal of this workflow? (e.g., "Submit an expense report")
- What inputs will change each time? (e.g., file path, date, amount)
- What is the expected outcome?

### Step 2: Start Recording

Run the recorder:

```
scripts\compile-and-start.ps1 -Name "<workflow-name>"
```

The recorder:
- Compiles the C# recording engine (one-time)
- Launches it as a hidden background process
- Captures every mouse click, key press, window title change
- Takes screenshots automatically after each action (max 1 per 400ms)
- Runs silently in the system tray

Confirm with the user that recording has started. Tell them to:

> "Recording is active. Please perform your workflow now. Everything you do will be
> captured. Press **Ctrl+Shift+F12** when you are done."

### Step 3: User Performs Workflow

The user performs their workflow normally. The recorder silently captures:

| Event | What's recorded |
|-------|----------------|
| Mouse click | Position (x,y), left/right button, active window |
| Key press | Key name (Enter, Tab, Ctrl+C, etc.), active window |
| Window switch | New window title |
| Screenshot | Full screen JPEG (every ~400ms) |

Recording continues until stopped.

### Step 4: Stop Recording

When the user finishes:

- **Option A**: They press **Ctrl+Shift+F12**
- **Option B**: Run:
  ```
  scripts\stop-recording.ps1 -Name "<workflow-name>"
  ```

The recorder saves the event log and generates a summary JSON.

### Step 5: Analyze and Generate Skill

Run the analysis script:

```
scripts\analyze-recording.ps1 -Name "<workflow-name>" -Description "<workflow goal>"
```

This:
1. Reads all captured events and screenshots
2. Groups them into sequential steps
3. Generates a complete skill at `skills/generated/<workflow-name>/`
4. The skill includes:
   - `SKILL.md` with frontmatter, step-by-step instructions, screenshots
   - `assets/screenshots/` — Reference images for each step
   - `assets/workflow-summary.json` — Recorded metadata

### Step 6: Install and Reuse

Copy the generated skill to the Codex skills directory:

```powershell
Copy-Item "skills/generated/<workflow-name>/skills/<workflow-name>" `
  -Destination "$env:USERPROFILE\.codex\skills\<workflow-name>\" -Recurse
```

Then in a new Codex thread, say:

> "Use **<workflow-name>** to [task description with new inputs]"

Codex reads the skill and replays the workflow automatically.

## Tips

- Keep the demonstration focused and natural — don't pause between steps.
- Use realistic placeholder values (the skill will use `{{placeholder}}` notation).
- Avoid entering passwords or secrets during recording. Use placeholders instead.
- If you make a mistake, stop and restart — don't try to edit the recording.
- After generation, review the skill and refine the descriptions for clarity.

## Troubleshooting

**"Recorder didn't start"**
- Check that Windows hook permissions are granted
- Run `compile-and-start.ps1` manually from a terminal to see errors

**"No events captured"**
- Make sure you clicked and typed during recording
- Check `assets/recordings/<name>/events.jsonl` exists and has content

**"Skill generation failed"**
- Verify the recording directory has events and screenshots
- Run `analyze-recording.ps1 -Name <name>` to see what was captured
