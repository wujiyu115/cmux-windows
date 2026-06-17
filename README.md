# cmux for Windows

[中文文档](README_CN.md)

A dark, keyboard-first terminal multiplexer for Windows, inspired by tmux/cmux workflows but built natively with WPF + ConPTY.

---

## Why / Who / What / How

| Why (problem) | Who (for) | What (feature) | How to use |
|---|---|---|---|
| You lose context across projects and shells | Developers juggling many repos/tasks | **Workspaces + surfaces (tabs)** | `Ctrl+N` new workspace, `Ctrl+T` new surface, switch with `Ctrl+1..9` |
| One terminal is never enough | CLI-heavy users, agent workflows | **Split panes** (right/down) | `Ctrl+D` split right, `Ctrl+Shift+D` split down, `Ctrl+Alt+Arrow` focus pane |
| You miss important agent outputs | AI-assisted coding users (Claude/Codex/etc.) | **OSC notifications + unread tracking** | `Ctrl+I` open notifications, `Ctrl+Shift+U` jump to latest unread |
| You need auditability of executed commands | Security-conscious / debugging workflows | **Command logs + history picker** | `Ctrl+Shift+L` logs, `Ctrl+Alt+H` command history, insert/run from UI |
| You want full session recall after crashes/restarts | Long-running sessions | **Session persistence + transcript capture** | Auto restore on startup + open **Session Vault** (`Ctrl+Shift+V`) |
| You want searchable output history like Termius vault | Anyone reviewing terminal sessions | **Session Vault browser** | Open vault, filter captures, preview transcript, copy/open file |
| You need dark theme consistency and personalization | Users who care about UX/readability | **19 built-in themes + custom colors** | Settings (`Ctrl+,`) for theme/font/cursor + workspace accents |
| You want quick actions without mouse hunting | Keyboard-first power users | **Command palette + shortcuts + chords** | `Ctrl+Shift+P` command palette, `Ctrl+K` chord sequences |
| You need automation from scripts/tools | Integrators/agent hooks | **Named pipe CLI API** (`cmux`) | `cmux notify`, `cmux workspace`, `cmux split`, `cmux status` |
| You want an AI assistant inside your terminal | AI-assisted coding users | **Built-in agent chat** (OpenAI / Anthropic) | `Ctrl+Shift+A` toggle agent panel, configure providers in Settings |
| You want per-project shell and env setup | Multi-project developers | **Project config** (`cmux.json`) | Place `.cmux/cmux.json` in project root for custom shell, env, color |
| You need to see what's listening | Devs running local servers | **Listening ports in sidebar** | Sidebar auto-shows TCP ports of child processes |

---

## Core capabilities

- Native **ConPTY terminal emulation** (real Windows terminal backend)
- Workspace sidebar with metadata (git branch, cwd, notifications, listening ports)
- Collapsible **workspace groups** in sidebar
- Multi-surface tabs and split-pane layout management (with preset layouts)
- **Built-in agent chat panel** (OpenAI / Anthropic providers, MCP servers, custom tools)
- Notification ingestion (OSC 9/99/777) for coding agents
- **Agent hook events** (stop, notification, session-start, permission-request, pre-tool-use)
- Command logs/history with filtering and quick replay
- **Snippets system** with templates (`{{key}}` placeholders), categories, tags, favorites
- Terminal transcript capture + Session Vault browsing
- Persistent sessions (window + workspace/surface/pane state, auto-save)
- **19 built-in terminal themes** (dark + light) + custom color editing
- **Shell profiles** (PowerShell, WSL, cmd — each with custom env and theme)
- **Project-level config** (`.cmux/cmux.json`) for per-project env, shell, color
- **Localization** (English / Chinese UI, switchable at runtime)
- **Browser panel** (WebView2) for web content alongside terminal
- Dark desktop UI with keyboard-first navigation + **chord shortcuts**
- CJK support (East Asian ambiguous-width configurable)

---

## Screenshots

<details>
  <summary>Open screenshots</summary>

  <p><strong>Main workspace view</strong></p>
  <img src="assets/screenshots/1.jpg" alt="cmux main workspace" width="1000" />

  <p><strong>Snippets panel</strong></p>
  <img src="assets/screenshots/2.jpg" alt="cmux snippets panel" width="700" />

  <p><strong>Command logs window</strong></p>
  <img src="assets/screenshots/3.jpg" alt="cmux command logs" width="1000" />
</details>

---

## Build and run (Windows)

### Requirements

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Optional: Visual Studio 2022 / Build Tools

### Clone

```powershell
git clone <repo-url> cmux-windows
cd cmux-windows
```

### Dev run

```powershell
dotnet build Cmux.sln -c Debug
dotnet run --project src/Cmux/Cmux.csproj -c Debug
```

---

## Build `.exe` on Windows

### Quick build (GUI + CLI)

```powershell
build.bat
```

Outputs `publish/cmuxw.exe` and `publish/cmux.exe`.

### 1) Framework-dependent `.exe` (smallest output)

```powershell
dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained false -o publish/cmux-win-x64
```

Output:
- `publish/cmux-win-x64/cmuxw.exe`

Use this when target machines already have .NET runtime installed.

### 2) Self-contained `.exe` (no runtime install needed)

```powershell
dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-win-x64-sc
```

Output:
- `publish/cmux-win-x64-sc/cmuxw.exe`

### 3) Single-file self-contained `.exe` (portable artifact)

```powershell
dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish/cmux-win-x64-single
```

Output:
- `publish/cmux-win-x64-single/cmuxw.exe`

> Note: WebView2-backed features may require WebView2 Runtime depending on target system state.

### Build CLI executable

```powershell
dotnet publish src/Cmux.Cli/Cmux.Cli.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-cli
```

Add `publish/cmux-cli` to `PATH` to use `cmux` globally.

---

## First 5 minutes (how to use)

1. Launch `cmuxw.exe`
2. `Ctrl+N` to create a workspace for your repo
3. `Ctrl+T` to create additional surfaces (tabs)
4. Split panes with `Ctrl+D` / `Ctrl+Shift+D`
5. Open command palette with `Ctrl+Shift+P` for quick actions
6. Open agent chat with `Ctrl+Shift+A`
7. Open logs with `Ctrl+Shift+L`
8. Open Session Vault with `Ctrl+Shift+V`
9. Open settings with `Ctrl+,` — pick a theme, configure agent providers, set shell profiles
10. Switch UI language in Settings (English / Chinese)

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

## Command palette actions

Open with `Ctrl+Shift+P`. Available actions include:

- Layout presets: **2-Column**, **3-Column**, **Grid**, **Main+Stack**
- **Equalize Panes** — make all split panes equal size
- **Test Notification** — verify notification pipeline
- **Agent Resume** — resume a previous agent conversation
- **Insert Last Command** — quick-insert most recent command from history

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

Place `.cmux/cmux.json` in your project root to configure per-project defaults:

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

## Shell profiles

Configure multiple shell profiles in Settings (`Ctrl+,`). Each profile can have:

- Custom command and arguments (e.g., `wsl`, `powershell`, `cmd`)
- Working directory
- Per-profile environment variables
- Theme override

---

## Built-in themes

19 terminal themes included — dark and light:

**Dark**: Default Dark, Dracula, Nord, Solarized Dark, One Dark, Monokai, Tokyo Night, Catppuccin Mocha, Gruvbox, Everforest Dark, Kanagawa, Ayu Dark

**Light**: Solarized Light, Catppuccin Latte, GitHub Light, Rose Pine Dawn, One Light, Tokyo Night Light, Everforest Light, Ayu Light, Nord Light, Gruvbox Light, Dracula Light

> Can also import themes from Ghostty config files (`~/.config/ghostty/config` or `%APPDATA%\ghostty\config`).

---

## Snippets

The Snippets panel lets you create reusable command templates:

- **Template placeholders**: use `{{key}}` syntax for parameter substitution
- Organize by **category** and **tags**
- **Favorites** and **use counting** for quick access
- Create, edit, delete, and insert snippets into the terminal

---

## Agent / AI chat

Built-in AI chat panel with full configuration in Settings:

- **Providers**: OpenAI-compatible and Anthropic endpoints (custom base URLs, models, API keys)
- **Custom tools**: define tool configs with name, description, command template
- **MCP servers**: Model Context Protocol integration (command, arguments, working directory)
- **Bash tool**: optional shell execution tool with configurable timeout
- **Web search**: Exa search integration
- **Conversation memory**: persistent threads with token tracking, auto-compaction
- **Streaming**: toggle streaming responses
- **Agent session resume**: restore previous agent sessions on restart
- **Secret storage**: API keys encrypted with Windows DPAPI

---

## Localization

Switch UI language at runtime in Settings:

- **English** (`en`)
- **Chinese** (`zh`)

---

## Architecture (high level)

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
