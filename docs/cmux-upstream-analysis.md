# cmux 上游项目深度分析

> 基于 [manaflow-ai/cmux](https://github.com/manaflow-ai/cmux) 源码探索，2026-06-16

---

## 项目概览

| 项目 | 详情 |
|------|------|
| **名称** | cmux |
| **描述** | 基于 Ghostty 的 macOS 终端，专为 AI 编程 Agent 设计 |
| **语言** | Swift (83%)，Python (6.7%)，TypeScript (4.7%)，Shell (1.8%)，Go (1.2%)，Rust (0.1%) |
| **Stars** | 22,147 |
| **Forks** | 1,717 |
| **License** | GPL-3.0 + 商业双许可 |
| **团队** | Manaflow, Inc.（2 位核心 + 1 位重要贡献者） |
| **官网** | https://cmux.com |
| **版本** | v0.64.16（每 2-3 天发版） |
| **活跃度** | 4,315 commits/年，每周 300-500 commits |

---

## 架构概览（大型 Monorepo，5500+ 文件）

| 目录 | 说明 | 文件数 |
|------|------|--------|
| `Sources/` | macOS 主应用（Swift/AppKit） | 551 |
| `Packages/` | 74 个 Swift Package（模块化组件） | 2,529 |
| `CLI/` | 命令行工具 | 32 |
| `web/` | Next.js 16 官网（文档/博客/Cloud VM） | 417 |
| `ios/` | iOS 伴侣应用 | 74 |
| `daemon/remote/` | 远程守护进程 cmuxd-remote（Go） | 26 |
| `ghostty` | Ghostty 子模块（Zig 构建，GPU 渲染） | — |
| `vendor/bonsplit` | 分割面板布局库 | — |
| `tests/` + `tests_v2/` | Python 集成测试 | 260 |
| `skills/` | Claude Code 技能定义 | 130 |
| `scripts/` | 构建/发布/CI 工具 | 105 |

### 关键 Swift Packages

| Package | 职责 |
|---------|------|
| CmuxCore / CmuxFoundation | 核心类型与工具 |
| CmuxTerminal / CmuxTerminalCore / CmuxTerminalEngine | 终端渲染 |
| CmuxBrowser / CmuxBrowserPanel | 内嵌浏览器 |
| CmuxControlSocket / CmuxSocketControl | Unix Socket API |
| CmuxNotifications | 通知系统 |
| CmuxSidebar / CmuxSidebarGit | 侧边栏与 Git 元数据 |
| CmuxPanes / CmuxWindowing / CmuxWorkspaces | 窗口/面板管理 |
| CmuxSettings / CmuxSettingsUI | 设置系统 |
| CmuxAgentChat / CmuxAgentChatUI | Agent 聊天界面 |
| CmuxRemoteDaemon / CmuxRemoteSession / CmuxRemoteWorkspace | SSH 远程 |
| CmuxCommandPalette | 命令面板 |
| CmuxSession | 会话持久化 |
| CmuxGit | Git 集成 |
| CmuxUpdater | Sparkle 自动更新 |
| CMUXAgentLaunch | Agent 启动与恢复 |
| CMUXWorkstream | Feed/审批工作流 |

---

## 一、通知系统

cmux 的通知系统是专为 AI 编程 Agent 设计的核心差异化功能。

### 1.1 三条触发路径

| 路径 | 机制 | 场景 |
|------|------|------|
| **Agent Hook** | `cmux hooks <agent> <event>` CLI 命令 | Agent 生命周期事件（停止、需要审批等） |
| **Feed/Workstream** | 阻塞式审批流（`feed.push` + Semaphore） | 工具权限请求、计划审查、问题确认 |
| **Socket API** | `notification.create` JSON-RPC | 外部工具直接创建通知 |

### 1.2 Agent Hook 工作流

```
AI Agent 触发事件（如 Claude Code 的 Stop）
  → Agent Hook 配置中的 shell 命令被执行
    → cmux CLI 解析 JSON stdin 载荷
      → 映射到 AgentHookAction（.stop / .notification / .sessionStart / .promptSubmit）
        → 通过 Unix Socket 发送命令到 cmux 应用
          → 通知存储、UI 更新、可选桌面横幅
```

### 1.3 支持 17 种 AI Agent

| Agent | 配置目录 | Hook 格式 | 关键事件 |
|-------|---------|-----------|---------|
| **Claude Code** | 内置 | Native | session-start, stop, notification, PermissionRequest |
| **Codex** | `~/.codex` | nested(5ms) | SessionStart, Stop, PreToolUse, PermissionRequest |
| **Gemini** | `~/.gemini` | nested(10s) | SessionStart, BeforeAgent, AfterAgent, PreToolUse |
| **Cursor** | `~/.cursor` | flat | beforeSubmitPrompt, stop, beforeShellExecution |
| **Grok** | `~/.grok/hooks` | nested(5s) | SessionStart, Stop, Notification, PreToolUse |
| **Kiro** | `~/.kiro/agents` | kiroAgentJSON(5s) | agentSpawn, stop, preToolUse |
| **Antigravity** | `~/.gemini/config` | antigravityJSON(10s) | SessionStart, Stop, PreToolUse |
| **Hermes Agent** | `~/.hermes` | hermesAgentYAML | on_session_start, pre_approval_request 等 |
| **Rovo Dev** | `~/.rovodev` | rovoDevYAML | on_complete, on_error, on_tool_permission |
| **Copilot** | `~/.copilot` | nested(5s) | SessionStart, Stop, PreToolUse |
| **CodeBuddy** | `~/.codebuddy` | nested(5s) | SessionStart, Stop, PreToolUse |
| **Factory** | `~/.factory` | nested(5s) | SessionStart, Stop, PreToolUse |
| **Qoder** | `~/.qoder` | nested(5s) | SessionStart, Stop, PreToolUse |
| **OpenCode** | `~/.config/opencode` | flat | — |
| **Pi** | `~/.pi/agent` | flat | — |
| **Amp** | `~/.config/amp` | flat | — |
| **OMP** | `~/.omp/agent` | flat | — |

### 1.4 Feed 事件分类

`FeedEventClassifier` 是判断 Hook 事件是否需要用户审批的核心逻辑：

- **有专用审批事件的 Agent**（Claude, Codex, Hermes）：`PreToolUse` 只是遥测，真正的审批走独立的 `PermissionRequest` / `pre_approval_request` 事件
- **无专用审批事件的 Agent**（Gemini, Copilot, Kiro 等）：副作用工具（Bash, Write, Edit, shell）升级为阻塞审批；只读工具（Read, Grep, Glob）保持遥测

阻塞审批流程：
1. CLI 发送 `feed.push` + `request_id` + `wait_timeout_seconds`
2. `FeedCoordinator.ingestBlocking()` 创建 `WorkstreamItem`，Hook 进程在 `DispatchSemaphore` 上等待
3. 应用未聚焦时弹出 `UNUserNotificationCenter` 横幅（Approve/Deny 按钮）
4. 侧边栏状态设为 "Needs input" + 蓝色 bell 图标 + `NSApp.requestUserAttention`

### 1.5 通知数据模型

```swift
struct TerminalNotification: Identifiable {
    let id: UUID
    let tabId: UUID           // 所属工作区
    let surfaceId: UUID?      // 所属终端面板
    let panelId: UUID?        // 面板
    let title, subtitle, body: String
    var isRead: Bool
    var paneFlash: Bool       // 面板闪烁
    var clickAction: TerminalNotificationClickAction?
}
```

`TerminalNotificationStore`（MainActor 单例）维护：
- `notifications` — 所有通知有序列表
- `manualUnreadWorkspaceIds` — 用户手动标记的未读
- `panelDerivedUnreadWorkspaceIds` — 面板活动产生的未读
- `restoredUnreadWorkspaceIds` — 会话恢复的未读
- `NotificationIndexes` — 快速索引（按工作区未读计数、最新未读等）

关键行为：
- **冷却去重**：通过 `cooldownKey` + `cooldownInterval` 节流重复通知
- **焦点抑制**：用户正在看目标面板时，抑制桌面横幅但仍记录通知
- **iOS 推送镜像**：通过 `PhonePushClient` 转发到配对的 iOS 设备

### 1.6 UI 表现（5 层）

| 层级 | 表现 | 控制 |
|------|------|------|
| **面板蓝圈** | 未读通知面板周围蓝色环 | `notificationPaneRingEnabled` |
| **面板闪烁** | 新通知时面板短暂闪烁 | `notificationPaneFlashEnabled` |
| **侧边栏高亮** | 未读计数 + 最新通知文本 + 状态条目 | `SidebarUnreadModel` |
| **Dock 角标** | 未读数量（上限 "99+"） | `notificationDockBadgeEnabled` |
| **系统横幅** | macOS 原生通知，带操作按钮 | `UNUserNotificationCenter` |

可选行为：**通知时工作区置顶**（`reorderOnNotification`）将收到通知的工作区浮动到侧边栏顶部。

### 1.7 Agent 身份识别

cmux **不通过进程检测**识别 Agent，而是通过 Hook 系统自带的身份：
- 环境变量 `CMUX_SURFACE_ID` / `CMUX_WORKSPACE_ID` / `CMUX_SOCKET_PATH`
- `ClaudeHookSessionStore` 持久化到 `~/.cmuxterm/<agent>-hook-sessions.json`
- 每个会话记录 `sessionId`, `workspaceId`, `surfaceId`, `pid`, `transcriptPath`
- `FeedJumpResolver` 反向映射 workstream_id 到工作区/面板

---

## 二、浏览器面板

### 2.1 渲染引擎

原生 **WKWebView**（Apple WebKit），非 Chromium/Electron。

`CmuxWebView`（WKWebView 子类）自定义功能：
- 粘贴为纯文本（注入 JS + WKScriptMessageHandler）
- 按键路由（App 菜单 ↔ WebKit ↔ 浏览器焦点）
- 右键菜单（下载图片、复制图片、新标签打开）
- 音频静音（私有 WebKit API `_setPageMuted:`）
- 隐藏标签页 WKWebView 300 秒后回收内存（`BrowserHiddenWebViewDiscardPolicy`）

### 2.2 完整脚本化 API（类 Playwright）

#### 导航

```bash
cmux browser open <url>                         # 在调用者工作区打开
cmux browser open <url> --workspace <id|ref>    # 指定工作区
cmux browser <surface> goto <url>
cmux browser <surface> back|forward|reload
cmux browser <surface> get url|title
```

#### 快照与检查（无障碍树）

```bash
cmux browser <surface> snapshot --interactive           # 完整无障碍树 + 元素引用
cmux browser <surface> snapshot --interactive --compact --max-depth 3
cmux browser <surface> get text body
cmux browser <surface> get html body
cmux browser <surface> get value "#email"
cmux browser <surface> get attr "#email" --attr placeholder
cmux browser <surface> get count ".row"
cmux browser <surface> get box "#submit"                # 边界框
cmux browser <surface> get styles "#submit" --property color
cmux browser <surface> eval '<js>'                      # 任意 JS 执行
```

#### 交互

```bash
cmux browser <surface> click|dblclick|hover|focus <selector-or-ref>
cmux browser <surface> fill <selector-or-ref> [text]    # 空文本清除
cmux browser <surface> type <selector-or-ref> <text>
cmux browser <surface> press|keydown|keyup <key>
cmux browser <surface> select <selector-or-ref> <value>
cmux browser <surface> check|uncheck <selector-or-ref>
cmux browser <surface> scroll [--selector <css>] [--dx N] [--dy N]
```

#### 等待

```bash
cmux browser <surface> wait --selector "#ready" --timeout-ms 10000
cmux browser <surface> wait --text "Done" --timeout-ms 10000
cmux browser <surface> wait --url-contains "/dashboard" --timeout-ms 10000
cmux browser <surface> wait --load-state complete --timeout-ms 15000
cmux browser <surface> wait --function "document.readyState === 'complete'" --timeout-ms 10000
```

#### 状态管理

```bash
cmux browser <surface> cookies get|set|clear
cmux browser <surface> storage local|session get|set|clear
cmux browser <surface> state save|load <path>     # 保存 cookies + localStorage + sessionStorage
cmux browser <surface> screenshot                  # 截图到剪贴板
```

### 2.3 Agent 与浏览器的交互模式

```
Agent 调用 snapshot --interactive
  → 生成带元素引用（e1, e2...）的无障碍树文本
    → Agent 读树理解页面结构
      → 通过引用执行操作：click e6, fill e10 "hello"
        → --snapshot-after 标志可一次往返返回新快照
          → DOM/导航变化后引用失效，需重新 snapshot
```

相比传统方式（完整 DOM → CSS 选择器猜测 → 操作），元素引用方式更可靠：
```
传统: full DOM → selector guessing → action
cmux: snapshot → refs (e1/e2/...) → direct action
```

### 2.4 SSH 远程路由

`RemoteDaemonProxyTunnel` 在本地创建 **SOCKS5 代理**（NWListener 绑定 127.0.0.1 动态端口），通过 SSH 隧道路由浏览器流量：

- `RemoteProxyBroker` 用租约模式共享一个代理隧道
- `RemoteLoopbackHTTPRequestRewriter` 重写 HTTP 头部（Host, Origin, Referer），将 loopback 别名替换为远程可达的 localhost
- `BrowserSystemProxyMirror` 处理 WebKit 不隐式绕过 loopback 的问题（Chromium 有此行为）

远程工作区中 `http://localhost:3000` 指向远程机器的 3000 端口。

### 2.5 面板集成

浏览器是面板系统的一等公民。`PanelType` 枚举包含 `.browser`（与 `.terminal`, `.markdown`, `.agentSession`, `.project` 等并列）。

- `BrowserPanelFocusIntent`：三种焦点目标（webView / addressBar / findField）
- `BrowserPaneDropTargetView`：拖拽支持（文件拖入、标签页重排、分割创建）
- `WindowBrowserPortal`：AppKit 层的 WebView 生命周期与几何同步

### 2.6 WKWebView 已知限制

无法实现（因缺少 Chrome DevTools Protocol）：
- 视口/设备模拟
- 离线模拟
- 录屏/截屏录制
- 网络拦截/Mock
- 低级原始输入注入（鼠标/键盘/触摸事件）
- 独立上下文代理控制

---

## 三、侧边栏与工作区系统

### 3.1 侧边栏架构

分层 Package 设计，数据模型、服务、UI 渲染严格分离：

| Package | 职责 |
|---------|------|
| CmuxSidebar | 纯数据模型 |
| CmuxSidebarGit | Git 分支检测 + PR 轮询 |
| CmuxSidebarProviderKit | 扩展 SDK |
| CmuxSidebarInterpreterService | 自定义侧边栏 DSL 解释器 |
| CmuxSwiftRender / CmuxSwiftRenderUI | 自定义渲染引擎 |

### 3.2 侧边栏信息层级

每个工作区显示 6 类辅助行，由 `SidebarWorkspaceAuxiliaryDetailVisibility` 控制：

```swift
public struct SidebarWorkspaceAuxiliaryDetailVisibility {
    public let showsMetadata: Bool        // 自定义元数据条目
    public let showsLog: Bool             // 最新日志行
    public let showsProgress: Bool        // Agent 进度条
    public let showsBranchDirectory: Bool // Git 分支 + 目录
    public let showsPullRequests: Bool    // PR 角标
    public let showsPorts: Bool           // 端口行
}
```

#### 状态条目（控制 Socket 报告）

```swift
public struct SidebarStatusEntry {
    public let key: String          // 稳定键（last-write-wins）
    public let value: String        // 显示文本
    public let icon: String?        // SF Symbol 名称
    public let color: String?       // 十六进制颜色
    public let url: URL?            // 可点击 URL
    public let priority: Int        // 排序优先级
    public let format: SidebarMetadataFormat  // .plain 或 .markdown
}
```

#### 日志与进度

- `SidebarLogEntry`：带严重级别（info / progress / success / warning / error）
- `SidebarProgressState`：进度条（0.0...1.0）+ 可选标签

### 3.3 工作区模型

#### 层级结构

```
Window
  → WorkspacesModel<Tab>（标签页列表 + 分组）
    → WorkspaceGroup（可折叠侧边栏分组）
      → PaneTreeModel<Panel>（面板注册表，Bonsplit 分割树）
        → Panel（终端 / 浏览器 / Markdown / Agent Chat 等）
```

#### WorkspacesModel

```swift
@MainActor @Observable
public final class WorkspacesModel<Tab: WorkspaceTabRepresenting> {
    public var tabs: [Tab] = []                    // 侧边栏顺序
    public var workspaceGroups: [WorkspaceGroup] = [] // 可折叠分组
    public var selectedTabId: UUID?                 // 活跃工作区
}
```

#### WorkspaceGroup

```swift
public struct WorkspaceGroup {
    public let id: UUID
    public var name: String
    public var isCollapsed: Bool
    public var isPinned: Bool
    public var anchorWorkspaceId: UUID   // 生命周期锚定工作区
    public var customColor: String?
    public var iconSymbol: String?
}
```

关闭锚定工作区解散组，其他成员保留。

#### 工作区定义（cmux.json）

```swift
struct CmuxWorkspaceDefinition: Codable {
    var name: String?
    var cwd: String?
    var color: String?
    var env: [String: String]?    // 工作区环境变量
    var layout: CmuxLayoutNode?   // 分割布局树
}
```

### 3.4 分割面板系统（Bonsplit）

`PaneTreeModel<Panel>` 是每个工作区的面板注册表：

```swift
@MainActor @Observable
public final class PaneTreeModel<Panel> {
    public var panels: [UUID: Panel] = [:]              // 面板注册
    public var paneLayoutVersion: Int = 0               // 拖拽重排版本号
    public var surfaceIdToPanelId: [TabID: UUID] = [:]  // Bonsplit → 面板映射
    public var lastOrderedPanelIds: [UUID] = []         // 空间顺序快照
}
```

`SplitLayoutModel<Transfer>` 管理分割/分离编排：
- `isProgrammaticSplit` — 程序化分割时抑制自动创建
- `detachingTabIds` — 正在分离的面板（用于跨工作区转移）
- 通过拦截关闭管道实现非破坏性面板转移

`PaneLayoutService` 提供两个操作：
1. **均等分割** — 遍历树，计算分割位置，子节点优先应用
2. **调整分割** — 按方向和像素量计算夹紧的分割位置

### 3.5 Git 集成

#### 分支检测（SidebarGitMetadataService）

- **初始探测**：重试序列 `[0, 0.5, 1.5, 3.0, 6.0, 10.0]` 秒，处理终端还没 cd 到 git 仓库的竞态
- **文件监视**：`RecursivePathWatcher` 监视 `.git/HEAD`, `.git/index` 等文件变化触发重探
- **脏检测优化**：跟踪 `indexSignature` / `indexContentSignature` / `headSignature`，避免 `git status` 更新 stat 缓存导致的误报
- **兜底重查**：5 分钟定时器
- **并发限制**：`WorkspaceGitMetadataProbeLimiter` 控制全局并发探测数

数据模型：`SidebarGitBranchState` 携带 `branch: String` + `isDirty: Bool`。`SidebarBranchOrdering` 按面板空间顺序去重分支，dirty-if-any-dirty 语义。

#### PR 关联（PullRequestPollService）

- **轮询频率**：选中工作区 10 秒，后台工作区 60 秒，±10% 抖动。终止态扫描每 15 分钟
- **流程**：git remote → 解析 GitHub slug → REST API 查询（60 秒仓库缓存）→ 分支匹配 PR
- **数据模型**：`SidebarPullRequestBadge` 携带 `number`, `url`, `status`(open/merged/closed), `branch`, `isStale`
- `main`/`master` 分支自动跳过 PR 查询

### 3.6 端口检测

通过 `SidebarWorkspaceAuxiliaryDetailVisibility.showsPorts` 控制。端口扫描可能基于 `CmuxTopProcessDetails` / `CmuxTopProcessEnumeration` / `CmuxTopProcessCPUTracker` 进程监控系统。

### 3.7 焦点管理

`FocusSurfaceBroadcaster` 精心设计的焦点广播组件：
- 永不同步交付（防止 `@Published` 重入）
- 合并多次发射为最新载荷
- 每 runloop 轮次最多 8 次交付（防止无限焦点循环）
- 载荷：`workspaceId`, `panelId`, `explicitFocusIntent`

### 3.8 扩展系统

完整的侧边栏扩展 SDK：
- `CmuxExtensionKit` — 扩展 API
- `CmuxSidebarProviderKit` — 侧边栏提供者协议
- 内置扩展示例：AttentionQueueSidebar, BrowserStackSidebar, DevServerSidebar, LastPromptSidebar, ProjectWorktreeSidebar, SuperCompactSidebar

---

## 四、SSH 远程工作区

### 4.1 三条入口

| 入口 | 方式 | 安全措施 |
|------|------|---------|
| CLI | `cmux ssh user@remote` | — |
| Deep Link | `ssh://`, `cmux://ssh` URL Scheme | 确认对话框 + "信任此目标"复选框 |
| UI | 直接创建 | 验证字符集、拒绝 loopback、长度限制 |

`CmuxSSHURLRequest` 验证：目标主机字符集、端口范围、SSH 选项、目标长度（256 字符）、标题长度（160 字符）、loopback 拒绝。

### 4.2 SSH 参数构建

```swift
func sshCommonArguments(batchMode: Bool) -> [String] {
    [
        "-o", "ConnectTimeout=6",
        "-o", "ServerAliveInterval=20",
        "-o", "ServerAliveCountMax=2",
        // StrictHostKeyChecking=accept-new（除非用户覆盖）
        // BatchMode=yes + ControlMaster=no（后台操作）
        // 端口、身份文件、用户 SSH 选项追加
    ]
}
```

### 4.3 远程守护进程 cmuxd-remote

#### 自动部署流程

1. **平台探测**：SSH 执行脚本获取 `$HOME`, `uname -s`, `uname -m`
2. **二进制检查**：`~/.cmux/bin/cmuxd-remote/<version>/<os>-<arch>/cmuxd-remote`
3. **获取来源**（优先级）：
   - `CMUX_REMOTE_DAEMON_BINARY` 环境变量（开发用）
   - App Bundle 内嵌清单 → GitHub Releases 下载 + SHA256 校验（CryptoKit）
   - 本地 `go build` 交叉编译兜底（开发用）
4. **上传安装**：`scp` → 临时路径 → `chmod 755 && mv` 原子安装
5. **能力握手**：`ssh <remote> cmuxd-remote serve --stdio` + JSON-RPC `hello`

#### 能力协商

| Capability | 说明 |
|------------|------|
| `proxy.stream.push` | 推式代理流（必需） |
| `pty.session` | 持久 PTY 会话 |
| `pty.session.token` | 令牌化 PTY 附加 |
| `pty.session.persistent_daemon` | SSH 断开后会话存活 |
| `pty.write.notification` | 写确认 |

#### 三种通信传输

| 传输 | 场景 |
|------|------|
| SSH stdio | 默认，换行分隔 JSON |
| SSH 本地转发 | Cloud VM（Socket `/run/cmuxd-remote.sock`） |
| WebSocket | 代理 Cloud VM 端点，5 秒心跳 |

RPC 客户端使用两个串行 DispatchQueue（状态 + 写入）+ 阻塞 Semaphore，**有意不用 Actor**（同步调用契约是代理隧道和 PTY 桥接的基础）。

#### 断线重连

指数退避：3 秒基底，倍增，60 秒上限。可达性探测：连续不可达时暂停自动重连，侧边栏显示手动"重连"按钮。

### 4.4 浏览器远程路由

`RemoteDaemonProxyTunnel` 在本地创建 SOCKS5 代理：

```
本地浏览器 → NWListener(127.0.0.1:动态端口)
  → RemoteDaemonProxySession → RPC Client → SSH 隧道
    → 远程 cmuxd-remote → 目标地址
```

- `RemoteProxyBroker`：租约模式共享代理隧道，自动重启 + 指数退避
- `RemoteLoopbackHTTPRequestRewriter`：重写 HTTP 请求行 + Host/Origin/Referer 头部
- `BrowserSystemProxyMirror`：WebKit 不隐式绕过 loopback，需显式 `excludedDomains`

### 4.5 SCP 拖拽上传

```
文件拖入远程工作区
  → 每个文件上传到 /tmp/cmux-drop-<uuid>[.ext]
    → scp -q -o ControlMaster=no（45 秒超时）
      → 失败/取消时回滚：sh -c 'rm -f -- /tmp/cmux-drop-xxx ...'
```

支持 `RemoteTransferCancelling` 协议，每个文件前检查取消状态。

### 4.6 tmux 兼容层

非完整 tmux -CC 协议实现，而是 cmux 自有工作区/面板模型的 tmux 风格翻译：
- 面板几何丰富化：`tmux_width`, `pane_height`, `pane_left`, `pane_top` 等
- `send-keys` 文本组合 + 特殊键映射（Enter, Tab, Ctrl+C/D/Z/L 等）
- Shell 引用与 `cd` 组合
- 面板启动命令检测

---

## 五、CLI 与控制 Socket

### 5.1 Unix Socket 协议

| 项 | 详情 |
|----|------|
| **主路径** | `~/.local/state/cmux/cmux.sock` |
| **兼容路径** | `/tmp/cmux.sock`, `/tmp/cmux-nightly.sock`, `/tmp/cmux-debug.sock` |
| **认证** | HMAC-SHA256 挑战-响应 / 密码认证（Keychain / 环境变量 / 文件） |
| **V1 协议** | 换行分隔文本命令（`send`, `set_status`, `notify` 等） |
| **V2 协议** | JSON-RPC（`method` + `params`），支持流式 |

Socket 路径解析（`CLISocketPathResolver`）：
- 标记文件面包屑跟踪每个 Bundle ID 的"最后 socket 路径"
- 非阻塞 `connect()` + `poll()` 探测活跃监听器
- 用户级 `cmux-<uid>.sock` 隔离

### 5.2 V2 Socket 方法全表

| 类别 | 方法 |
|------|------|
| **Workspace** | `workspace.create`, `workspace.rename`, `workspace.move_to_window`, `workspace.list`, `workspace.action` |
| **Surface** | `surface.split_off`, `surface.drag_to_split`, `surface.move`, `surface.reorder`, `surface.action`, `surface.send_text`, `surface.send_key`, `surface.list`, `surface.read_text` |
| **Pane** | `pane.resize`, `pane.swap`, `pane.break`, `pane.join`, `pane.list` |
| **Tab** | `tab.action` |
| **Window** | `window.list` |
| **File** | `file.open` |
| **Browser** | `browser.open_split`, `browser.navigate`, `browser.back`, `browser.forward`, `browser.reload`, `browser.click`, `browser.dblclick`, `browser.hover`, `browser.focus`, `browser.press`, `browser.keydown`, `browser.keyup`, `browser.type`, `browser.fill`, `browser.check`, `browser.uncheck`, `browser.select`, `browser.scroll`, `browser.scroll_into_view` |
| **Notification** | `notification.create`, `notification.clear`, `notification.dismiss`, `notification.mark_read`, `notification.open`, `notification.jump_to_unread` |
| **Feed** | `feed.permission.reply`, `feed.question.reply`, `feed.exit_plan.reply` |
| **App** | `app.focus_override.set`, `app.simulate_active` |
| **Settings** | `settings.open` |
| **System** | `system.memory`, `events.stream`, `reload_config` |
| **Remotes** | `remotes.list`, `remotes.add`, `remotes.remove` |

V1 侧边栏命令：`set_status`, `report_meta`, `clear_status`, `clear_meta`, `set_progress`, `clear_progress`, `log`, `clear_log`, `reset_sidebar`

### 5.3 CLI 命令

| 命令 | 说明 |
|------|------|
| `cmux open <path/url>` | 打开文件、目录、URL |
| `cmux diff [patch]` | Diff 查看器 |
| `cmux config doctor/get/set/reload` | 配置管理 |
| `cmux themes [list/set/clear]` | 主题管理（交互式 TUI 选择器） |
| `cmux events` | SSE 风格事件流 |
| `cmux memory` | 内存诊断 |
| `cmux remotes list/add/remove` | iOS 设备管理 |
| `cmux settings [open <target>]` | 打开设置到指定区域 |
| `cmux shortcuts` | 键盘快捷键设置 |
| `cmux docs [topic]` | 文档参考 |
| `cmux top` | 进程监控 |
| `cmux ssh <target>` | SSH 远程工作区 |
| `cmux hooks <agent> <event>` | Agent Hook 处理 |
| `cmux browser <surface> <cmd>` | 浏览器自动化 |
| `cmux notify` | 发送通知 |

### 5.4 主题系统

- 写入 Ghostty 托管配置：`~/Library/Application Support/<bundleId>/config`
- 格式：`theme = light:<theme>,dark:<theme>`（双模式）或单主题名
- 托管标记：`# cmux themes start` / `# cmux themes end`

主题源搜索（优先级）：
1. `GHOSTTY_RESOURCES_DIR/themes/`
2. App Bundle `Resources/ghostty/themes/`
3. Xcode 构建输出
4. `XDG_DATA_DIRS/ghostty/themes/`
5. `/Applications/Ghostty.app/Contents/Resources/ghostty/themes/`
6. `~/.config/ghostty/themes/`
7. Application Support 主题目录

热重载：`DistributedNotificationCenter` 发送 `com.cmuxterm.themes.reload-config`

### 5.5 命令面板

三层架构：

| 层 | 技术 | 说明 |
|----|------|------|
| **模糊搜索** | Rust FFI（`nucleo` crate） | ASCII 位掩码预过滤 + 首字母匹配（14K 基分）+ 关键词精确匹配（30K+ 奖励）+ 模糊评分 |
| **命令注册** | Swift | 50+ 命令，`CommandPaletteCommandContribution`（commandId + 本地化标题 + 关键词 + 处理闭包） |
| **渲染** | SwiftUI | `LazyVStack`，24pt 行高，450pt 最大高度，字符级高亮匹配，yield-based 合并更新 |

### 5.6 键盘快捷键

```swift
struct StoredShortcut {
    // 单键：解析自 "modifier+key"（如 "cmd+shift+t"）
    // 和弦序列：解析自数组（如 ["cmd+k", "cmd+t"]）
    // 显式解绑：""（空字符串）
}
```

cmux.json 配置：
```json
{
  "actions": {
    "myAction": {
      "shortcut": "cmd+shift+t",
      "title": "My Action",
      "command": "echo hello"
    },
    "myChord": {
      "shortcut": ["cmd+k", "cmd+t"],
      "builtin": "newTerminal"
    }
  }
}
```

### 5.7 设置架构

- **文件格式**：JSONC（JSON with comments）
- **文件位置**（优先级）：
  1. `~/.config/cmux/cmux.json`（主）
  2. `.cmux/cmux.json` 或 `cmux.json`（项目级，向上查找到 `$HOME`）
  3. `~/Library/Application Support/<bundleId>/config`（兼容）
  4. `~/.config/ghostty/config`（只读）

`CmuxConfigStore`（@MainActor @ObservableObject）：加载、合并、文件监视、发布解析后的配置。本地配置优先于全局。`.synced` 视图合并所有配置文件并标注来源。

`cmux config doctor`：验证所有配置文件的 JSONC 语法，报告键数量、字节大小、错误。

---

## 六、会话持久化与恢复

### 6.1 快照存储

`SessionSnapshotRepository`（文件 JSON 存储）：

| 项 | 详情 |
|----|------|
| **路径** | `~/Library/Application Support/cmux/session-<bundleId>.json` |
| **备份** | `session-<bundleId>-previous.json` |
| **格式** | JSON，排序键 |
| **写入** | 原子写入 + 内容不变时跳过 |
| **版本** | Schema 版本检查，不匹配视为不可用 |
| **恢复** | 主文件损坏时回退到 `-previous` 备份 |
| **生命周期** | `applicationWillTerminate` 同步保存；自动保存在私有串行队列 |

### 6.2 保存内容

- 窗口数量与位置
- 工作区布局（标签页）
- 面板树（分割面板）
- 终端滚动缓冲区文本（有字符数限制 `maxScrollbackCharactersPerTerminal`）
- 浏览器 URL 与导航历史
- Markdown 面板状态
- 状态条目、日志条目、进度条目
- Git 分支

### 6.3 Agent 会话恢复

`AgentResumeArgv` 为 16+ 种 Agent 构建恢复命令：

| Agent | 恢复命令 |
|-------|---------|
| Claude Code | `claude --resume <sessionId>`（通过 wrapper shim） |
| Codex | `codex resume <sessionId>` |
| Grok | `grok -r <sessionId>` |
| Pi | `pi --session <sessionId>` |
| Amp | `amp threads continue <sessionId>` |
| Cursor | `cursor-agent --resume <sessionId>` |
| Gemini | `gemini --resume <sessionId>` |
| OpenCode | `opencode --session <sessionId>` |
| Copilot | `copilot --resume <sessionId>` |
| Kiro, Antigravity, RovoDev, Hermes, CodeBuddy, Factory, Qoder | 各自格式 |

**Claude Code wrapper shim**：`CMUX_CLAUDE_WRAPPER_SHIM` 环境变量指向每个 surface 的 shim，确保恢复的 Claude 会话重新注入 hooks。POSIX 兼容的 shell 命令包装。

**工作目录解析策略**：
- **目录命名空间型 Agent**（claude, grok, pi, cursor, gemini, copilot）：使用**启动**工作目录（稳定，匹配会话存储命名空间）
- **ID 键型 Agent**（codex, opencode, amp, antigravity, rovodev）：使用**运行时** CWD

**安全限制**：
- 只有受信任的绑定（活跃进程检测到的 tmux 绑定或用户批准的前缀）才自动运行
- 敏感环境键（tokens, passwords, secrets, API keys）被剥离

### 6.4 恢复流程

```
退出时：写入版本化 JSON 快照
  → 重新启动：读取并验证快照（版本检查、非空窗口检查）
    → 重建窗口/工作区/面板布局
      → 恢复终端滚动缓冲区、浏览器 URL、工作目录
        → autoResumeAgentSessions 启用时（默认）运行各 Agent 的 resume 命令
```

手动恢复：File → Reopen Previous Session / Cmd+Shift+O / `cmux restore-session`

后台预加载：`BackgroundWorkspacePrimeCoordinator` 2 秒超时内预加载恢复的工作区。

---

## 七、关键架构模式

| 模式 | 说明 |
|------|------|
| **协议缝合** | 每个子系统暴露协议（`SidebarGitMetadataServing`, `SessionSnapshotStoring` 等），单一生产实现，支持测试注入 |
| **willSet/didSet 观察器** | 不用 Combine `@Published` 或 async stream，用同步属性观察器委托到宿主协议，保持精确时序 |
| **MainActor 隔离** | 所有状态机 `@MainActor`，离主线程工作通过 `Task.detached(priority: .utility)` |
| **控制 Socket 线协议** | 侧边栏数据类型用冻结 `String` 原始值作为线协议 |
| **Bonsplit 库** | 分割面板树的专用库，`CmuxPanes` 提供工作区侧映射层 |
| **Rust FFI** | 命令面板模糊搜索使用 Rust `nucleo` crate 通过 C ABI |

---

## 八、与 cmux-windows 的关系

| 维度 | cmux (macOS) | cmux-windows |
|------|-------------|-------------|
| **平台** | macOS | Windows |
| **语言** | Swift / AppKit / SwiftUI | C# / WPF |
| **终端引擎** | libghostty（Zig，GPU 加速） | ConPTY + 自研渲染 |
| **浏览器** | WKWebView | — |
| **IPC** | Unix Socket（JSON-RPC） | Named Pipe（守护进程） |
| **主题** | Ghostty 主题文件 | TerminalThemes.cs + AppThemeService |
| **侧边栏** | 扩展 SDK + 自定义 DSL | WorkspaceSidebarItem.xaml |
| **会话恢复** | SessionSnapshotRepository | SessionPersistenceService |
| **通知** | 17 Agent Hook + Feed 系统 | — |
| **i18n** | 20+ 语言 | 中/英双语 |
