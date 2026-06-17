# cmux for Windows

[English](README.md)

一款深色、键盘优先的 Windows 终端多路复用器，灵感来自 tmux/cmux 工作流，基于 WPF + ConPTY 原生构建。

---

## 项目介绍

cmux 是一款原生 Windows 终端多路复用器，将 tmux 风格的工作区管理带到 Windows 桌面。它结合了真正的 ConPTY 终端后端与 WPF 深色界面、内置 AI 代理聊天、会话持久化和 CLI 自动化 — 全部键盘优先，全部本地运行。

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

## 功能特性

### 工作区与布局

- 原生 **ConPTY 终端模拟**（真正的 Windows 终端后端）
- 工作区侧边栏，显示元数据（git 分支、当前目录、通知、监听端口）
- 可折叠的**工作区分组**
- 多表面标签页和**分屏布局**，含预设布局（两列、三列、网格、主+堆栈）
- **片段系统** — 可复用命令模板，支持 `{{key}}` 占位符、分类、标签、收藏

### 会话与审计

- **会话持久化** — 自动保存和恢复（窗口 + 工作区/表面/面板状态）
- **命令日志 + 历史选择器** — 筛选、插入、重放
- **终端转录捕获** + 会话保险库浏览（可搜索的输出历史）
- 通知接收（OSC 9/99/777），用于编码代理

### 代理 / AI 聊天

- **内置代理聊天面板**（`Ctrl+Shift+A`） — OpenAI / Anthropic 服务商
- **自定义工具** — 定义工具配置（名称、描述、命令模板）
- **MCP 服务器** — Model Context Protocol 集成（命令、参数、工作目录）
- **Bash 工具** — 可选 shell 执行，可配置超时
- **Web 搜索** — Exa 集成
- **对话记忆** — 持久线程，带 token 追踪和自动压缩
- **代理会话恢复** — 重启时恢复之前的代理会话
- **密钥存储** — API 密钥使用 Windows DPAPI 加密
- **代理钩子事件** — stop、notification、session-start、permission-request、pre-tool-use

### 主题与个性化

- **19 套内置终端主题**（12 深色 + 7 浅色）：Dracula、Nord、One Dark、Monokai、Tokyo Night、Catppuccin Mocha、Gruvbox、Everforest、Kanagawa、Ayu、Solarized、GitHub Light、Rose Pine Dawn 等
- 自定义配色编辑 + 工作区强调色
- **Shell 配置** — PowerShell、WSL、cmd，每个配置可设环境变量和主题覆盖
- **项目级配置**（`.cmux/cmux.json`） — 按项目设置 shell、环境、颜色、图标
- **Ghostty 配置导入** — 从 `ghostty/config` 读取主题、字体、字号
- **浏览器面板**（WebView2）在终端旁显示网页内容

### 键盘优先

- **命令面板**（`Ctrl+Shift+P`） — 模糊搜索、布局预设、均分面板、测试通知、恢复代理会话
- **组合键快捷键**（`Ctrl+K` 后按第二个键，超时可配置）
- CJK 支持 — 东亚歧义宽度可配置

### 国际化

- **英文** / **中文**界面 — 在设置中运行时切换

---

## 快速上手

1. 启动 `cmuxw.exe`
2. `Ctrl+N` — 新建工作区
3. `Ctrl+T` — 新建表面（标签页）
4. `Ctrl+D` / `Ctrl+Shift+D` — 分屏
5. `Ctrl+Shift+P` — 命令面板快速操作
6. `Ctrl+Shift+A` — 切换代理聊天面板
7. `Ctrl+Shift+L` — 命令日志
8. `Ctrl+Shift+V` — 会话保险库
9. `Ctrl+,` — 设置（主题、代理服务商、Shell 配置、语言）

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

## 开发指南

### 系统要求

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 可选：Visual Studio 2022 / Build Tools

### 克隆与开发运行

```powershell
git clone <repo-url> cmux-windows
cd cmux-windows
dotnet build Cmux.sln -c Debug
dotnet run --project src/Cmux/Cmux.csproj -c Debug
```

### 构建 `.exe`

快速构建（GUI + CLI）：

```powershell
build.bat
```

输出 `publish/cmuxw.exe` 和 `publish/cmux.exe`。

其他选项：

| 类型 | 命令 | 输出 |
|---|---|---|
| 框架依赖 | `dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained false -o publish/cmux-win-x64` | `cmuxw.exe`（最小，需 .NET 运行时） |
| 自包含 | `dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-win-x64-sc` | `cmuxw.exe`（无需运行时） |
| 单文件 | `dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish/cmux-win-x64-single` | `cmuxw.exe`（便携） |
| 仅 CLI | `dotnet publish src/Cmux.Cli/Cmux.Cli.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-cli` | `cmux.exe`（添加到 `PATH`） |

> 注意：WebView2 相关功能可能需要 WebView2 运行时。

### 架构

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
