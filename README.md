# Workflow Recorder — Codex Plugin for Windows

> Record desktop workflows by demonstration and turn them into reusable Codex skills.
> **Windows equivalent of macOS Record & Replay.**

[![Plugin Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/pephupephu/codex-workflow-recorder)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-brightgreen.svg)]()
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## How it works

1. **Start recording** — Codex launches a hidden C# background process
2. **Work normally** — Every mouse click, key press, and window switch is automatically captured with screenshots
3. **Stop recording** — Press `Ctrl+Shift+F12`
4. **Skill generated** — Codex analyzes the recording and produces a reusable `.codex/skills/` skill
5. **Replay anytime** — In a new thread, ask Codex to use the generated skill

## Features

- ✅ **Global mouse hooks** — Captures every left/right click with screen coordinates
- ✅ **Global keyboard hooks** — Captures key presses (Enter, Tab, Ctrl+C, numpad, etc.)
- ✅ **Window tracking** — Records active window titles for context
- ✅ **Auto screenshots** — Takes full-screen screenshots after each action
- ✅ **Ctrl+Shift+F12** — Global hotkey to stop recording instantly
- ✅ **No Python required** — Pure PowerShell + C# (compiled via built-in `csc.exe`)
- ✅ **Skill generation** — Automatically creates a reusable Codex skill from recording

## Installation

### Prerequisites

- Windows 10 or Windows 11
- .NET Framework 4.8+ (built-in on Windows 10/11)
- Codex Desktop App (latest version)

### Option 1: Codex Marketplace (recommended)

1. Open **Codex Desktop App**
2. Go to **Plugins → Personal**
3. Click **Install** on **Workflow Recorder**
4. Restart Codex

### Option 2: Manual Install

```powershell
# Clone the repo
git clone https://github.com/pephupephu/codex-workflow-recorder.git
cd codex-workflow-recorder

# Copy to plugins folder (adjust path to your actual plugin path)
Copy-Item ".\workflow-recorder" "$env:USERPROFILE\plugins\" -Recurse -Force
```

### Option 3: Team/Enterprise — Shared Repo Marketplace

Add `.agents/plugins/marketplace.json` to your team repo:

```json
{
  "name": "team-workflow",
  "interface": { "displayName": "Team Tools" },
  "plugins": [
    {
      "name": "workflow-recorder",
      "source": { "source": "local", "path": "./plugins/workflow-recorder" },
      "policy": { "installation": "AVAILABLE", "authentication": "ON_INSTALL" },
      "category": "Productivity"
    }
  ]
}
```

Team members then run:
```powershell
codex plugin marketplace add <path-to-team-repo-root>
```

## Plugin Structure

```
workflow-recorder/
├── .codex-plugin/
│   └── plugin.json              ← Plugin manifest (name, version, skills)
├── .gitignore
├── README.md
├── skills/
│   ├── record-workflow/
│   │   └── SKILL.md             ← Core recording instruction skill
│   └── generated/               ← User-generated skills (gitignored)
│       └── .gitkeep
├── scripts/
│   ├── Recorder.cs              ← C# recording engine (global hooks)
│   ├── compile-and-start.ps1    ← Compile + start recording
│   ├── stop-recording.ps1       ← Stop recording (Ctrl+Shift+F12)
│   ├── analyze-recording.ps1    ← Analyze + generate skill
│   └── list-recordings.ps1      ← List recorded sessions
└── assets/
    └── recordings/              ← Saved recordings (gitignored)
        └── .gitkeep
```

## Usage

### Record a Workflow

In a Codex thread, say:

> *"Use record-workflow, I want to record how I submit an expense report"*

Codex will:
1. Ask about the workflow goal
2. Start the hidden C# recorder
3. Tell you to perform your workflow
4. Wait for you to press `Ctrl+Shift+F12` when done
5. Analyze the recording and generate a reusable skill

### Replay a Recorded Workflow

In a new Codex thread, say:

> *"Use [workflow-name] to [task description with new inputs]"*

For example:
> *"Use expense-report to submit a report with C:\receipt.pdf, category Travel, $45.50"*

## How the Recording Engine Works

The `Recorder.cs` uses Windows **low-level global hooks** (`WH_MOUSE_LL` and `WH_KEYBOARD_LL`) to silently observe all user input. It:

1. **Listens** for mouse clicks and key presses via Windows hooks
2. **Captures** the active window title via `GetForegroundWindow()` + `GetWindowText()`
3. **Screenshots** the full screen using `Graphics.CopyFromScreen()`
4. **Logs** every event to a structured JSONL file
5. **Saves** screenshots as PNG files
6. **Responds** to `stop.signal` file to cleanly terminate

The recorder runs as a **hidden console process** — no window, no tray icon, zero user distraction.

## Requirements

- **OS**: Windows 10 or Windows 11
- **Runtime**: .NET Framework 4.8+ (pre-installed)
- **Codex**: Desktop app (latest version)
- **Memory**: ~10 MB while recording

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

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m "Add amazing feature"`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

MIT License — see [LICENSE](LICENSE) for details.

## Author

[pephupephu](https://github.com/pephupephu)
