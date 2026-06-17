# cmux for Windows

[中文文档](README_CN.md)

A dark, keyboard-first terminal multiplexer for Windows, inspired by tmux/cmux workflows but built natively with WPF + ConPTY.

---

## Introduction

cmux is a native Windows terminal multiplexer that brings tmux-style workspace management to the Windows desktop. It combines a real ConPTY terminal backend with a WPF-based dark UI, built-in AI agent chat, session persistence, and a CLI for automation — all keyboard-first, all local.

<details>
  <summary>Screenshots</summary>

  <p><strong>Main workspace view</strong></p>
  <img src="assets/screenshots/1.jpg" alt="cmux main workspace" width="1000" />

  <p><strong>Snippets panel</strong></p>
  <img src="assets/screenshots/2.jpg" alt="cmux snippets panel" width="700" />

  <p><strong>Command logs window</strong></p>
  <img src="assets/screenshots/3.jpg" alt="cmux command logs" width="1000" />
</details>

---

## Features

### Workspace & layout

- Native **ConPTY terminal emulation** (real Windows terminal backend)
- Workspace sidebar with metadata (git branch, cwd, notifications, listening ports)
- Collapsible **workspace groups** in sidebar
- Multi-surface tabs and **split-pane layout** with preset layouts (2-column, 3-column, grid, main+stack)
- **Snippets system** — reusable command templates with `{{key}}` placeholders, categories, tags, favorites

### Session & audit

- **Session persistence** — auto-save and restore (window + workspace/surface/pane state)
- **Command logs + history picker** — filter, insert, replay
- **Terminal transcript capture** + Session Vault browsing (searchable output history)
- Notification ingestion (OSC 9/99/777) for coding agents

### Agent / AI chat

- **Built-in agent chat panel** (`Ctrl+Shift+A`) — OpenAI / Anthropic providers
- **Custom tools** — define tool configs with name, description, command template
- **MCP servers** — Model Context Protocol integration (command, arguments, working directory)
- **Bash tool** — optional shell execution with configurable timeout
- **Web search** — Exa integration
- **Conversation memory** — persistent threads with token tracking and auto-compaction
- **Agent session resume** — restore previous agent sessions on restart
- **Secret storage** — API keys encrypted with Windows DPAPI
- **Agent hook events** — stop, notification, session-start, permission-request, pre-tool-use

### Theming & personalization

- **19 built-in terminal themes** (12 dark + 7 light): Dracula, Nord, One Dark, Monokai, Tokyo Night, Catppuccin Mocha, Gruvbox, Everforest, Kanagawa, Ayu, Solarized, GitHub Light, Rose Pine Dawn, etc.
- Custom color editing + workspace accent colors
- **Shell profiles** — PowerShell, WSL, cmd with per-profile env vars and theme override
- **Project-level config** (`.cmux/cmux.json`) — per-project shell, env, color, icon
- **Ghostty config import** — read `ghostty/config` for theme, font, size
- **Browser panel** (WebView2) for web content alongside terminal

### Keyboard-first

- **Command palette** (`Ctrl+Shift+P`) — fuzzy search, layout presets, equalize panes, test notification, agent resume
- **Chord shortcuts** (`Ctrl+K` then second key within configurable timeout)
- CJK support — East Asian ambiguous-width configurable

### Localization

- **English** / **Chinese** UI — switchable at runtime in Settings

---

## Quick start

1. Launch `cmuxw.exe`
2. `Ctrl+N` — new workspace
3. `Ctrl+T` — new surface (tab)
4. `Ctrl+D` / `Ctrl+Shift+D` — split panes
5. `Ctrl+Shift+P` — command palette for quick actions
6. `Ctrl+Shift+A` — toggle agent chat panel
7. `Ctrl+Shift+L` — command logs
8. `Ctrl+Shift+V` — session vault
9. `Ctrl+,` — settings (theme, agent providers, shell profiles, language)

---

## Keyboard shortcuts

### Workspaces

| Shortcut | Action |
|---|---|
| `Ctrl+N` | New workspace |
| `Ctrl+1..8` | Jump to workspace 1..8 |
| `Ctrl+9` | Jump to last workspace |
| `Ctrl+Shift+W` | Close workspace |
| `Ctrl+Shift+R` | Rename workspace |
| `Ctrl+B` | Toggle sidebar |

### Surfaces (tabs)

| Shortcut | Action |
|---|---|
| `Ctrl+T` | New surface |
| `Ctrl+W` | Close surface |
| `Ctrl+Shift+]` | Next surface |
| `Ctrl+Shift+[` | Previous surface |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Cycle surfaces |

### Panes

| Shortcut | Action |
|---|---|
| `Ctrl+D` | Split right |
| `Ctrl+Shift+D` | Split down |
| `Ctrl+Alt+Arrow` | Focus adjacent pane |
| `Ctrl+Shift+Z` | Zoom/unzoom pane |

### Agent & Productivity

| Shortcut | Action |
|---|---|
| `Ctrl+Shift+A` | Toggle agent chat panel |
| `Ctrl+Shift+P` | Command palette |
| `Ctrl+Shift+F` | Search overlay |
| `Ctrl+Shift+L` | Command logs |
| `Ctrl+Shift+V` | Session vault |
| `Ctrl+Alt+H` | Command history picker |
| `Ctrl+Shift+H` | Insert last command from history |
| `Ctrl+Backspace` | Delete word backward |
| `Ctrl+,` | Settings |

### Chord shortcuts

Press `Ctrl+K`, then a second key within 500ms (configurable via `ChordTimeoutMs`):

| Chord | Action |
|---|---|
| `Ctrl+K, Ctrl+T` | New surface |
| `Ctrl+K, Ctrl+W` | Close workspace |

---

## CLI usage

```powershell
# Send a notification (e.g., from agent hooks)
cmux notify --title "Claude Code" --body "Waiting for input" --subtitle "hook"

# Agent hook events
cmux hooks claude-code stop
cmux hooks claude-code notification
echo '{"agent":"claude-code","event":"stop"}' | cmux hooks

# Stream real-time events as JSON
cmux events

# Workspace management
cmux workspace list
cmux workspace create --name "My Project"
cmux workspace select --index 0
cmux workspace next
cmux workspace previous

# Surface/pane actions
cmux surface create
cmux surface next
cmux surface previous
cmux split right
cmux split down

# Sidebar status entries
cmux status set --key "build" --value "passing" --icon "✓" --color "#4caf50"
cmux status clear --key "build"
cmux status

# Shell completion
cmux completion powershell

# Version
cmux version
```

---

## Project configuration

Place `.cmux/cmux.json` in your project root for per-project defaults:

```json
{
  "name": "my-project",
  "cwd": "C:\\dev\\my-project",
  "color": "#ff6b6b",
  "icon": "📁",
  "shell": "wsl",
  "startDirectory": "~/projects/my-project",
  "env": {
    "NODE_ENV": "development",
    "DATABASE_URL": "postgres://localhost:5432/mydb"
  }
}
```

---

## Development guide

### Requirements

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Optional: Visual Studio 2022 / Build Tools

### Clone & dev run

```powershell
git clone <repo-url> cmux-windows
cd cmux-windows
dotnet build Cmux.sln -c Debug
dotnet run --project src/Cmux/Cmux.csproj -c Debug
```

### Build `.exe`

Quick build (GUI + CLI):

```powershell
build.bat
```

Outputs `publish/cmuxw.exe` and `publish/cmux.exe`.

Other options:

| Type | Command | Output |
|---|---|---|
| Framework-dependent | `dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained false -o publish/cmux-win-x64` | `cmuxw.exe` (smallest, needs .NET runtime) |
| Self-contained | `dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-win-x64-sc` | `cmuxw.exe` (no runtime needed) |
| Single-file | `dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish/cmux-win-x64-single` | `cmuxw.exe` (portable) |
| CLI only | `dotnet publish src/Cmux.Cli/Cmux.Cli.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-cli` | `cmux.exe` (add to `PATH`) |

> Note: WebView2-backed features may require WebView2 Runtime.

### Architecture

```text
src/
  Cmux/         WPF desktop app (views, controls, themes, agent chat, snippets)
  Cmux.Core/    terminal engine, models, services, persistence, IPC, agent
  Cmux.Cli/     command-line client for automation
tests/
  Cmux.Tests/   unit tests
```

---

## License

MIT
