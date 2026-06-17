# cmux for Windows

[English](README.md)

一款深色、键盘优先的 Windows 终端多路复用器，灵感来自 tmux/cmux 工作流，基于 WPF + ConPTY 原生构建。

---

## 为什么 / 适用人群 / 功能 / 使用方法

| 为什么（问题） | 适用人群 | 功能 | 使用方法 |
|---|---|---|---|
| 在多个项目和 shell 之间容易丢失上下文 | 管理多个仓库/任务的开发者 | **工作区 + 表面（标签页）** | `Ctrl+N` 新建工作区，`Ctrl+T` 新建表面，`Ctrl+1..9` 切换 |
| 一个终端永远不够 | 重度 CLI 用户、AI 辅助工作流 | **分屏**（右/下） | `Ctrl+D` 右分屏，`Ctrl+Shift+D` 下分屏，`Ctrl+Alt+方向键` 切换焦点 |
| 容易错过 AI 代理的输出 | AI 编码用户（Claude/Codex 等） | **OSC 通知 + 未读追踪** | `Ctrl+I` 打开通知，`Ctrl+Shift+U` 跳到最新未读 |
| 需要对执行的命令进行审计 | 安全敏感/调试工作流 | **命令日志 + 历史选择器** | `Ctrl+Shift+L` 日志，`Ctrl+Alt+H` 命令历史，从 UI 插入/运行 |
| 希望崩溃/重启后恢复完整会话 | 长时间运行的会话 | **会话持久化 + 捕获转录** | 启动时自动恢复 + 打开**会话保险库**（`Ctrl+Shift+V`） |
| 需要可搜索的输出历史（类似 Termius vault） | 任何需要回顾终端会话的用户 | **会话保险库浏览器** | 打开保险库，筛选捕获，预览转录，复制/打开文件 |
| 需要深色主题的一致性和个性化 | 注重用户体验/可读性的用户 | **19 套内置主题 + 自定义配色** | 设置（`Ctrl+,`）选择主题/字体/光标 + 工作区强调色 |
| 想要快速操作而不找鼠标 | 键盘优先的高级用户 | **命令面板 + 快捷键 + 组合键** | `Ctrl+Shift+P` 命令面板，`Ctrl+K` 组合键序列 |
| 需要从脚本/工具进行自动化 | 集成者/代理钩子 | **命名管道 CLI API**（`cmux`） | `cmux notify`，`cmux workspace`，`cmux split`，`cmux status` |
| 想在终端内使用 AI 助手 | AI 辅助编码用户 | **内置代理聊天**（OpenAI / Anthropic） | `Ctrl+Shift+A` 切换代理面板，在设置中配置服务商 |
| 想要按项目定制 shell 和环境变量 | 多项目开发者 | **项目配置**（`cmux.json`） | 在项目根目录放置 `.cmux/cmux.json`，自定义 shell、环境、颜色 |
| 需要查看哪些端口在监听 | 运行本地服务器的开发者 | **侧边栏显示监听端口** | 侧边栏自动显示子进程的 TCP 监听端口 |

---

## 核心能力

- 原生 **ConPTY 终端模拟**（真正的 Windows 终端后端）
- 工作区侧边栏，显示元数据（git 分支、当前目录、通知、监听端口）
- 可折叠的**工作区分组**
- 多表面标签页和分屏布局管理（含预设布局）
- **内置代理聊天面板**（OpenAI / Anthropic 服务商，MCP 服务器，自定义工具）
- 通知接收（OSC 9/99/777），用于编码代理
- **代理钩子事件**（stop、notification、session-start、permission-request、pre-tool-use）
- 命令日志/历史，带过滤和快速重放
- **片段系统**，支持模板（`{{key}}` 占位符）、分类、标签、收藏
- 终端转录捕获 + 会话保险库浏览
- 持久化会话（窗口 + 工作区/表面/面板状态，自动保存）
- **19 套内置终端主题**（深色 + 浅色）+ 自定义配色编辑
- **Shell 配置**（PowerShell、WSL、cmd — 各带自定义环境变量和主题）
- **项目级配置**（`.cmux/cmux.json`）按项目设置环境变量、shell、颜色
- **国际化**（英文 / 中文界面，运行时可切换）
- **浏览器面板**（WebView2）在终端旁显示网页内容
- 深色桌面 UI，键盘优先导航 + **组合键快捷键**
- CJK 支持（东亚歧义宽度可配置）

---

## 截图

<details>
  <summary>展开截图</summary>

  <p><strong>主工作区视图</strong></p>
  <img src="assets/screenshots/1.jpg" alt="cmux 主工作区" width="1000" />

  <p><strong>片段面板</strong></p>
  <img src="assets/screenshots/2.jpg" alt="cmux 片段面板" width="700" />

  <p><strong>命令日志窗口</strong></p>
  <img src="assets/screenshots/3.jpg" alt="cmux 命令日志" width="1000" />
</details>

---

## 构建和运行（Windows）

### 系统要求

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 可选：Visual Studio 2022 / Build Tools

### 克隆

```powershell
git clone <repo-url> cmux-windows
cd cmux-windows
```

### 开发运行

```powershell
dotnet build Cmux.sln -c Debug
dotnet run --project src/Cmux/Cmux.csproj -c Debug
```

---

## 在 Windows 上构建 `.exe`

### 快速构建（GUI + CLI）

```powershell
build.bat
```

输出 `publish/cmuxw.exe` 和 `publish/cmux.exe`。

### 1) 框架依赖 `.exe`（最小输出）

```powershell
dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained false -o publish/cmux-win-x64
```

输出：
- `publish/cmux-win-x64/cmuxw.exe`

适用于目标机器已安装 .NET 运行时的场景。

### 2) 自包含 `.exe`（无需安装运行时）

```powershell
dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-win-x64-sc
```

输出：
- `publish/cmux-win-x64-sc/cmuxw.exe`

### 3) 单文件自包含 `.exe`（便携产物）

```powershell
dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish/cmux-win-x64-single
```

输出：
- `publish/cmux-win-x64-single/cmuxw.exe`

> 注意：WebView2 相关功能可能需要 WebView2 运行时，取决于目标系统状态。

### 构建 CLI 可执行文件

```powershell
dotnet publish src/Cmux.Cli/Cmux.Cli.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-cli
```

将 `publish/cmux-cli` 添加到 `PATH` 即可全局使用 `cmux` 命令。

---

## 快速上手（5 分钟）

1. 启动 `cmuxw.exe`
2. `Ctrl+N` 为你的仓库创建工作区
3. `Ctrl+T` 创建额外的表面（标签页）
4. 用 `Ctrl+D` / `Ctrl+Shift+D` 分屏
5. 用 `Ctrl+Shift+P` 打开命令面板快速操作
6. 用 `Ctrl+Shift+A` 打开代理聊天面板
7. 用 `Ctrl+Shift+L` 打开日志
8. 用 `Ctrl+Shift+V` 打开会话保险库
9. 用 `Ctrl+,` 打开设置 — 选择主题，配置代理服务商，设置 Shell 配置
10. 在设置中切换界面语言（英文 / 中文）

---

## 键盘快捷键

### 工作区

| 快捷键 | 操作 |
|---|---|
| `Ctrl+N` | 新建工作区 |
| `Ctrl+1..8` | 跳转到工作区 1..8 |
| `Ctrl+9` | 跳转到最后一个工作区 |
| `Ctrl+Shift+W` | 关闭工作区 |
| `Ctrl+Shift+R` | 重命名工作区 |
| `Ctrl+B` | 切换侧边栏 |

### 表面（标签页）

| 快捷键 | 操作 |
|---|---|
| `Ctrl+T` | 新建表面 |
| `Ctrl+W` | 关闭表面 |
| `Ctrl+Shift+]` | 下一个表面 |
| `Ctrl+Shift+[` | 上一个表面 |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | 循环切换表面 |

### 面板

| 快捷键 | 操作 |
|---|---|
| `Ctrl+D` | 右分屏 |
| `Ctrl+Shift+D` | 下分屏 |
| `Ctrl+Alt+方向键` | 焦点移到相邻面板 |
| `Ctrl+Shift+Z` | 放大/缩小面板 |

### 代理与效率

| 快捷键 | 操作 |
|---|---|
| `Ctrl+Shift+A` | 切换代理聊天面板 |
| `Ctrl+Shift+P` | 命令面板 |
| `Ctrl+Shift+F` | 搜索覆盖层 |
| `Ctrl+Shift+L` | 命令日志 |
| `Ctrl+Shift+V` | 会话保险库 |
| `Ctrl+Alt+H` | 命令历史选择器 |
| `Ctrl+Shift+H` | 插入上一条命令 |
| `Ctrl+Backspace` | 向前删除一个词 |
| `Ctrl+,` | 设置 |

### 组合键快捷键

先按 `Ctrl+K`，再在 500ms 内按第二个键（超时可配置 `ChordTimeoutMs`）：

| 组合键 | 操作 |
|---|---|
| `Ctrl+K, Ctrl+T` | 新建表面 |
| `Ctrl+K, Ctrl+W` | 关闭工作区 |

---

## 命令面板操作

用 `Ctrl+Shift+P` 打开。可用操作包括：

- 预设布局：**两列**、**三列**、**网格**、**主+堆栈**
- **均分面板** — 让所有分屏面板等宽
- **测试通知** — 验证通知管道是否正常
- **恢复代理会话** — 继续之前的代理对话
- **插入上一条命令** — 快速插入最近一条历史命令

---

## CLI 使用

```powershell
# 发送通知（例如从代理钩子）
cmux notify --title "Claude Code" --body "等待输入" --subtitle "hook"

# 代理钩子事件
cmux hooks claude-code stop
cmux hooks claude-code notification
echo '{"agent":"claude-code","event":"stop"}' | cmux hooks

# 实时事件流（JSON 格式）
cmux events

# 工作区管理
cmux workspace list
cmux workspace create --name "我的项目"
cmux workspace select --index 0
cmux workspace next
cmux workspace previous

# 表面/面板操作
cmux surface create
cmux surface next
cmux surface previous
cmux split right
cmux split down

# 侧边栏状态条目
cmux status set --key "build" --value "passing" --icon "✓" --color "#4caf50"
cmux status clear --key "build"
cmux status

# Shell 补全
cmux completion powershell

# 版本信息
cmux version
```

---

## 项目配置

在项目根目录放置 `.cmux/cmux.json` 配置项目默认设置：

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

## Shell 配置

在设置（`Ctrl+,`）中配置多个 Shell 配置。每个配置可以有：

- 自定义命令和参数（如 `wsl`、`powershell`、`cmd`）
- 工作目录
- 每个配置的环境变量
- 主题覆盖

---

## 内置主题

包含 19 套终端主题 — 深色和浅色：

**深色**：Default Dark、Dracula、Nord、Solarized Dark、One Dark、Monokai、Tokyo Night、Catppuccin Mocha、Gruvbox、Everforest Dark、Kanagawa、Ayu Dark

**浅色**：Solarized Light、Catppuccin Latte、GitHub Light、Rose Pine Dawn、One Light、Tokyo Night Light、Everforest Light、Ayu Light、Nord Light、Gruvbox Light、Dracula Light

> 也可以从 Ghostty 配置文件导入主题（`~/.config/ghostty/config` 或 `%APPDATA%\ghostty\config`）。

---

## 片段

片段面板支持创建可复用的命令模板：

- **模板占位符**：使用 `{{key}}` 语法进行参数替换
- 按**分类**和**标签**组织
- **收藏**和**使用计数**方便快速访问
- 创建、编辑、删除、插入片段到终端

---

## 代理 / AI 聊天

内置 AI 聊天面板，在设置中完整配置：

- **服务商**：OpenAI 兼容和 Anthropic 端点（自定义 base URL、模型、API 密钥）
- **自定义工具**：定义工具配置（名称、描述、命令模板）
- **MCP 服务器**：Model Context Protocol 集成（命令、参数、工作目录）
- **Bash 工具**：可选 shell 执行工具，可配置超时
- **Web 搜索**：Exa 搜索集成
- **对话记忆**：持久线程，带 token 追踪和自动压缩
- **流式输出**：可开关流式响应
- **代理会话恢复**：重启时恢复之前的代理会话
- **密钥存储**：API 密钥使用 Windows DPAPI 加密存储

---

## 国际化

在设置中运行时切换界面语言：

- **英文**（`en`）
- **中文**（`zh`）

---

## 架构（概览）

```text
src/
  Cmux/         WPF 桌面应用（视图、控件、主题、代理聊天、片段）
  Cmux.Core/    终端引擎、模型、服务、持久化、IPC、代理
  Cmux.Cli/     命令行客户端，用于自动化
tests/
  Cmux.Tests/   单元测试
```

---

## 许可证

MIT
