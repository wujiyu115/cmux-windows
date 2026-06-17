# cmux for Windows

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
| 需要深色主题的一致性和个性化 | 注重用户体验/可读性的用户 | **深色 UI + 终端主题定制** | 设置（`Ctrl+,`）调整颜色/字体/光标 + 工作区强调色 |
| 想要快速操作而不找鼠标 | 键盘优先的高级用户 | **命令面板 + 快捷键** | `Ctrl+Shift+P` 命令面板，菜单镜像核心流程 |
| 需要从脚本/工具进行自动化 | 集成者/代理钩子 | **命名管道 CLI API**（`cmux`） | `cmux notify`，`cmux workspace`，`cmux split`，`cmux status` |

---

## 核心能力

- 原生 **ConPTY 终端模拟**（真正的 Windows 终端后端）
- 工作区侧边栏，显示元数据（git 分支、当前目录、通知）
- 多表面标签页和分屏布局管理
- 通知接收（OSC 9/99/777），用于编码代理
- 带过滤和快速重放的命令日志/历史
- 终端转录捕获 + 会话保险库浏览
- 持久化会话（窗口 + 工作区/表面/面板状态）
- 深色桌面 UI，键盘优先导航

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
6. 用 `Ctrl+Shift+L` 打开日志
7. 用 `Ctrl+Shift+V` 打开会话保险库
8. 用 `Ctrl+,` 打开设置，调整终端主题/字体/光标

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

### 效率

| 快捷键 | 操作 |
|---|---|
| `Ctrl+Shift+P` | 命令面板 |
| `Ctrl+Shift+F` | 搜索覆盖层 |
| `Ctrl+Shift+L` | 命令日志 |
| `Ctrl+Shift+V` | 会话保险库 |
| `Ctrl+Alt+H` | 命令历史选择器 |
| `Ctrl+,` | 设置 |

---

## CLI 使用

```powershell
# 发送通知（例如从代理钩子）
cmux notify --title "Claude Code" --body "等待输入"

# 工作区管理
cmux workspace list
cmux workspace create --name "我的项目"
cmux workspace select --index 0

# 表面/面板操作
cmux surface create
cmux split right
cmux split down

# 查看状态
cmux status
```

---

## 架构（概览）

```text
src/
  Cmux/         WPF 桌面应用（视图、控件、主题）
  Cmux.Core/    终端引擎、模型、服务、持久化、IPC
  Cmux.Cli/     命令行客户端，用于自动化
tests/
  Cmux.Tests/   单元测试
```

---

## 许可证

MIT
