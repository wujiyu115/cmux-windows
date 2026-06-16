using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cmux.Core.Config;
using Cmux.Core.IPC;
using Cmux.Core.Models;
using Cmux.Core.Services;
using Cmux.Services;

namespace Cmux.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<WorkspaceViewModel> _workspaces = [];

    [ObservableProperty]
    private WorkspaceViewModel? _selectedWorkspace;

    [ObservableProperty]
    private ObservableCollection<WorkspaceGroup> _workspaceGroups = new();

    [ObservableProperty]
    private bool _sidebarVisible = true;

    [ObservableProperty]
    private double _sidebarWidth = 280;

    [ObservableProperty]
    private bool _compactSidebar;

    private double _sidebarWidthBeforeCompact = 280;

    public bool IsSidebarExpanded => !CompactSidebar;

    [ObservableProperty]
    private bool _notificationPanelVisible;

    [ObservableProperty]
    private int _totalUnreadCount;

    [ObservableProperty]
    private bool _agentPanelVisible = SettingsService.Current.AgentChatDefaultOpen;

    [ObservableProperty]
    private double _agentPanelWidth = 380;

    private string? _workspaceFilter;

    public List<object> SidebarItems
    {
        get
        {
            var items = new List<object>();
            var query = _workspaceFilter?.Trim();
            bool hasFilter = !string.IsNullOrWhiteSpace(query);

            bool MatchesFilter(WorkspaceViewModel ws) =>
                !hasFilter
                || (ws.Name?.Contains(query!, StringComparison.OrdinalIgnoreCase) ?? false)
                || (ws.WorkingDirectory?.Contains(query!, StringComparison.OrdinalIgnoreCase) ?? false)
                || (ws.GitBranch?.Contains(query!, StringComparison.OrdinalIgnoreCase) ?? false)
                || (ws.AgentLabel?.Contains(query!, StringComparison.OrdinalIgnoreCase) ?? false);

            // Ungrouped workspaces first
            foreach (var ws in Workspaces.Where(w => w.Workspace.GroupId == null && MatchesFilter(w)))
                items.Add(ws);
            // Grouped workspaces
            foreach (var group in WorkspaceGroups)
            {
                var groupWorkspaces = Workspaces.Where(w => w.Workspace.GroupId == group.Id && MatchesFilter(w)).ToList();
                if (!hasFilter || groupWorkspaces.Count > 0)
                {
                    items.Add(group);
                    if (!group.IsCollapsed)
                    {
                        foreach (var ws in groupWorkspaces)
                            items.Add(ws);
                    }
                }
            }
            return items;
        }
    }

    public void SetWorkspaceFilter(string? filter)
    {
        _workspaceFilter = filter;
        OnPropertyChanged(nameof(SidebarItems));
    }

    public void CreateGroup(string name)
    {
        var group = new WorkspaceGroup { Name = name };
        WorkspaceGroups.Add(group);
        OnPropertyChanged(nameof(SidebarItems));
    }

    public void DeleteGroup(string groupId)
    {
        var group = WorkspaceGroups.FirstOrDefault(g => g.Id == groupId);
        if (group == null) return;
        foreach (var ws in Workspaces.Where(w => w.Workspace.GroupId == groupId))
            ws.Workspace.GroupId = null;
        WorkspaceGroups.Remove(group);
        OnPropertyChanged(nameof(SidebarItems));
    }

    public void ToggleGroupCollapsed(string groupId)
    {
        var group = WorkspaceGroups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            group.IsCollapsed = !group.IsCollapsed;
            OnPropertyChanged(nameof(SidebarItems));
        }
    }

    public void MoveWorkspaceToGroup(WorkspaceViewModel ws, string? groupId)
    {
        ws.Workspace.GroupId = groupId;
        OnPropertyChanged(nameof(SidebarItems));
    }

    public void RefreshSidebarItems() => OnPropertyChanged(nameof(SidebarItems));

    private readonly NotificationService _notificationService;

    public NotificationService NotificationService => _notificationService;

    public EventBus EventBus { get; } = new();

    public MainViewModel()
    {
        _notificationService = App.NotificationService;
        _notificationService.UnreadCountChanged += () =>
        {
            TotalUnreadCount = _notificationService.UnreadCount;
            UpdateWorkspaceNotificationCounts();
        };
        _notificationService.NotificationAdded += n =>
        {
            EventBus.Publish("notification.added", new { title = n.Title, body = n.Body });
        };

        // Wire up the named pipe command handler
        if (App.PipeServer != null)
        {
            App.PipeServer.OnCommand = HandlePipeCommand;
            App.PipeServer.OnStreamCommand = HandleEventStream;
        }

        // Restore session or create default workspace
        var session = SessionPersistenceService.Load();
        if (session != null && session.Workspaces.Count > 0)
        {
            RestoreSession(session);
        }
        else
        {
            CreateNewWorkspace();
        }
    }

    [RelayCommand]
    public void CreateNewWorkspace()
    {
        var workspace = new Workspace { Name = LanguageService.Lang("Default_Workspace", Workspaces.Count + 1) };
        var surface = new Surface { Name = LanguageService.Lang("Default_Terminal1") };
        workspace.Surfaces.Add(surface);
        workspace.SelectedSurface = surface;

        var vm = new WorkspaceViewModel(workspace, _notificationService);
        Workspaces.Add(vm);
        SelectedWorkspace = vm;
        OnPropertyChanged(nameof(SidebarItems));
        EventBus.Publish("workspace.created", new { id = workspace.Id, name = workspace.Name });
    }

    public void DuplicateWorkspace(WorkspaceViewModel source)
    {
        var clone = new Workspace
        {
            Name = source.Name + LanguageService.Lang("Tab_Copy"),
            IconGlyph = source.IconGlyph,
            AccentColor = source.AccentColor,
            WorkingDirectory = source.WorkingDirectory,
            StartDirectory = source.StartDirectory,
            EnvironmentVariables = new Dictionary<string, string>(source.Workspace.EnvironmentVariables),
        };

        var surfaceMap = new Dictionary<string, Surface>();

        foreach (var sourceSurfaceVm in source.Surfaces)
        {
            var sourceSurface = sourceSurfaceVm.Surface;
            var paneIdMap = new Dictionary<string, string>();
            var clonedRoot = CloneSplitNode(sourceSurface.RootSplitNode, paneIdMap);

            var clonedSurface = new Surface
            {
                Name = sourceSurface.Name,
                RootSplitNode = clonedRoot,
                FocusedPaneId = sourceSurface.FocusedPaneId != null && paneIdMap.TryGetValue(sourceSurface.FocusedPaneId, out var mappedFocused)
                    ? mappedFocused
                    : clonedRoot.GetLeaves().Select(l => l.PaneId).FirstOrDefault(),
            };

            foreach (var (oldPaneId, customName) in sourceSurface.PaneCustomNames)
            {
                if (paneIdMap.TryGetValue(oldPaneId, out var newPaneId))
                    clonedSurface.PaneCustomNames[newPaneId] = customName;
            }

            foreach (var (oldPaneId, snapshot) in sourceSurface.PaneSnapshots)
            {
                if (!paneIdMap.TryGetValue(oldPaneId, out var newPaneId))
                    continue;

                clonedSurface.PaneSnapshots[newPaneId] = new PaneStateSnapshot
                {
                    CapturedAt = snapshot.CapturedAt,
                    WorkingDirectory = snapshot.WorkingDirectory,
                    Shell = snapshot.Shell,
                    CommandHistory = snapshot.CommandHistory.ToList(),
                    BufferSnapshot = snapshot.BufferSnapshot == null
                        ? null
                        : new Cmux.Core.Terminal.TerminalBufferSnapshot
                        {
                            Cols = snapshot.BufferSnapshot.Cols,
                            Rows = snapshot.BufferSnapshot.Rows,
                            CursorRow = snapshot.BufferSnapshot.CursorRow,
                            CursorCol = snapshot.BufferSnapshot.CursorCol,
                            ScrollbackLines = snapshot.BufferSnapshot.ScrollbackLines.ToList(),
                            ScreenLines = snapshot.BufferSnapshot.ScreenLines.ToList(),
                        },
                };
            }

            clone.Surfaces.Add(clonedSurface);
            surfaceMap[sourceSurface.Id] = clonedSurface;
        }

        clone.SelectedSurface = source.SelectedSurface != null && surfaceMap.TryGetValue(source.SelectedSurface.Surface.Id, out var selected)
            ? selected
            : clone.Surfaces.FirstOrDefault();

        if (clone.Surfaces.Count == 0)
        {
            var fallbackSurface = new Surface { Name = LanguageService.Lang("Default_Terminal1") };
            clone.Surfaces.Add(fallbackSurface);
            clone.SelectedSurface = fallbackSurface;
        }

        var vm = new WorkspaceViewModel(clone, _notificationService);
        Workspaces.Add(vm);
        SelectedWorkspace = vm;
        OnPropertyChanged(nameof(SidebarItems));
    }

    [RelayCommand]
    public void CloseWorkspace(WorkspaceViewModel? workspace)
    {
        if (workspace == null) return;
        if (Workspaces.Count <= 1) return; // Keep at least one

        var closedId = workspace.Workspace.Id;
        var closedName = workspace.Name;
        int index = Workspaces.IndexOf(workspace);
        workspace.CaptureAllSurfaceTranscripts("workspace-close");
        workspace.Dispose();
        Workspaces.Remove(workspace);
        OnPropertyChanged(nameof(SidebarItems));
        EventBus.Publish("workspace.closed", new { id = closedId, name = closedName });

        if (SelectedWorkspace == workspace)
        {
            SelectedWorkspace = Workspaces[Math.Min(index, Workspaces.Count - 1)];
        }
    }

    [RelayCommand]
    public void SelectWorkspace(int index)
    {
        if (index >= 0 && index < Workspaces.Count)
        {
            SelectedWorkspace = Workspaces[index];
        }
    }

    [RelayCommand]
    public void NextWorkspace()
    {
        if (Workspaces.Count == 0) return;
        int index = SelectedWorkspace != null ? Workspaces.IndexOf(SelectedWorkspace) : -1;
        SelectedWorkspace = Workspaces[(index + 1) % Workspaces.Count];
    }

    [RelayCommand]
    public void PreviousWorkspace()
    {
        if (Workspaces.Count == 0) return;
        int index = SelectedWorkspace != null ? Workspaces.IndexOf(SelectedWorkspace) : 0;
        SelectedWorkspace = Workspaces[(index - 1 + Workspaces.Count) % Workspaces.Count];
    }

    [RelayCommand]
    public void ToggleSidebar() => SidebarVisible = !SidebarVisible;

    [RelayCommand]
    public void ToggleCompactSidebar() => CompactSidebar = !CompactSidebar;

    [RelayCommand]
    public void ToggleNotificationPanel() => NotificationPanelVisible = !NotificationPanelVisible;

    [RelayCommand]
    public void ToggleAgentPanel() => AgentPanelVisible = !AgentPanelVisible;

    partial void OnCompactSidebarChanged(bool value)
    {
        if (value)
        {
            if (SidebarWidth > 120)
                _sidebarWidthBeforeCompact = SidebarWidth;

            SidebarWidth = 92;
        }
        else
        {
            SidebarWidth = Math.Max(220, _sidebarWidthBeforeCompact);
        }

        OnPropertyChanged(nameof(IsSidebarExpanded));
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceViewModel? value)
    {
        if (value != null)
        {
            EventBus.Publish("workspace.selected", new { id = value.Workspace.Id, name = value.Name });
        }
    }

    [RelayCommand]
    public void JumpToLatestUnread()
    {
        var latest = _notificationService.GetLatestUnread();
        if (latest == null) return;

        // Find the workspace and surface
        var workspace = Workspaces.FirstOrDefault(w => w.Workspace.Id == latest.WorkspaceId);
        if (workspace != null)
        {
            SelectedWorkspace = workspace;
            var surface = workspace.Surfaces.FirstOrDefault(s => s.Surface.Id == latest.SurfaceId);
            if (surface != null)
            {
                workspace.SelectedSurface = surface;
                if (latest.PaneId != null)
                {
                    surface.FocusPane(latest.PaneId);
                    surface.FlashPaneAttention(latest.PaneId);
                }
            }
            _notificationService.MarkAsRead(latest.Id);
        }
    }

    [RelayCommand]
    public void MarkAllNotificationsRead()
    {
        _notificationService.MarkAllAsRead();
    }

    private void UpdateWorkspaceNotificationCounts()
    {
        foreach (var ws in Workspaces)
        {
            ws.UnreadNotificationCount = _notificationService.GetUnreadCount(ws.Workspace.Id);
            ws.LatestNotificationText = _notificationService.GetLatestText(ws.Workspace.Id);

            foreach (var surface in ws.Surfaces)
                surface.RefreshNotificationState();
        }
    }

    public void SaveSession(double windowX, double windowY, double windowWidth, double windowHeight, bool isMaximized)
    {
        // Capture terminal transcripts and in-memory terminal context before serializing.
        foreach (var workspace in Workspaces)
        {
            foreach (var surface in workspace.Surfaces)
            {
                surface.CaptureAllPaneTranscripts("session-close");
                surface.CapturePaneSnapshotsForPersistence();
            }
        }

        var workspaces = Workspaces.Select(w => w.Workspace).ToList();
        var state = SessionPersistenceService.BuildState(
            workspaces,
            SelectedWorkspace != null ? Workspaces.IndexOf(SelectedWorkspace) : null,
            windowX, windowY, windowWidth, windowHeight,
            isMaximized, SidebarWidth, SidebarVisible, CompactSidebar);

        // Persist agent session IDs (stored on ViewModels, not models)
        for (int i = 0; i < Workspaces.Count && i < state.Workspaces.Count; i++)
        {
            state.Workspaces[i].AgentSessionId = Workspaces[i].AgentSessionId;
            state.Workspaces[i].AgentSessionAgent = Workspaces[i].AgentSessionAgent;
        }

        // Persist workspace groups
        if (WorkspaceGroups.Count > 0)
            state.WorkspaceGroups = WorkspaceGroups.ToList();

        SessionPersistenceService.Save(state);
    }

    private void RestoreSession(SessionState session)
    {
        foreach (var wsState in session.Workspaces)
        {
            var workspace = new Workspace
            {
                Id = wsState.Id,
                Name = wsState.Name,
                IconGlyph = string.IsNullOrWhiteSpace(wsState.IconGlyph) ? "\uE8A5" : wsState.IconGlyph,
                AccentColor = string.IsNullOrWhiteSpace(wsState.AccentColor) ? "#FF818CF8" : wsState.AccentColor,
                WorkingDirectory = wsState.WorkingDirectory,
                StartDirectory = wsState.StartDirectory,
                EnvironmentVariables = wsState.EnvironmentVariables ?? new(),
                GroupId = wsState.GroupId,
            };

            foreach (var surfState in wsState.Surfaces)
            {
                var surface = new Surface
                {
                    Id = surfState.Id,
                    Name = surfState.Name,
                    FocusedPaneId = surfState.FocusedPaneId,
                    PaneCustomNames = new Dictionary<string, string>(surfState.PaneCustomNames),
                    PaneSnapshots = surfState.PaneSnapshots.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new PaneStateSnapshot
                        {
                            CapturedAt = kvp.Value.CapturedAt,
                            WorkingDirectory = kvp.Value.WorkingDirectory,
                            Shell = kvp.Value.Shell,
                            CommandHistory = kvp.Value.CommandHistory.ToList(),
                            BufferSnapshot = kvp.Value.BufferSnapshot == null
                                ? null
                                : new Cmux.Core.Terminal.TerminalBufferSnapshot
                                {
                                    Cols = kvp.Value.BufferSnapshot.Cols,
                                    Rows = kvp.Value.BufferSnapshot.Rows,
                                    CursorRow = kvp.Value.BufferSnapshot.CursorRow,
                                    CursorCol = kvp.Value.BufferSnapshot.CursorCol,
                                    ScrollbackLines = kvp.Value.BufferSnapshot.ScrollbackLines.ToList(),
                                    ScreenLines = kvp.Value.BufferSnapshot.ScreenLines.ToList(),
                                },
                        }),
                };

                if (surfState.RootNode != null)
                {
                    surface.RootSplitNode = SessionPersistenceService.DeserializeSplitNode(surfState.RootNode);
                }

                workspace.Surfaces.Add(surface);
            }

            if (wsState.SelectedSurfaceIndex.HasValue &&
                wsState.SelectedSurfaceIndex.Value >= 0 &&
                wsState.SelectedSurfaceIndex.Value < workspace.Surfaces.Count)
            {
                workspace.SelectedSurface = workspace.Surfaces[wsState.SelectedSurfaceIndex.Value];
            }
            else if (workspace.Surfaces.Count > 0)
            {
                workspace.SelectedSurface = workspace.Surfaces[0];
            }

            var vm = new WorkspaceViewModel(workspace, _notificationService);
            vm.AgentSessionId = wsState.AgentSessionId;
            vm.AgentSessionAgent = wsState.AgentSessionAgent;
            Workspaces.Add(vm);
        }

        // Restore workspace groups
        if (session.WorkspaceGroups != null)
        {
            foreach (var group in session.WorkspaceGroups)
                WorkspaceGroups.Add(group);
        }

        // Resume agent sessions after a delay
        foreach (var ws in Workspaces)
        {
            if (ws.AgentSessionId != null)
                ws.TryResumeAgentSession();
        }

        if (session.SelectedWorkspaceIndex.HasValue &&
            session.SelectedWorkspaceIndex.Value >= 0 &&
            session.SelectedWorkspaceIndex.Value < Workspaces.Count)
        {
            SelectedWorkspace = Workspaces[session.SelectedWorkspaceIndex.Value];
        }
        else if (Workspaces.Count > 0)
        {
            SelectedWorkspace = Workspaces[0];
        }

        if (session.Window != null)
        {
            SidebarWidth = Math.Clamp(session.Window.SidebarWidth, 220, 500);
            SidebarVisible = session.Window.SidebarVisible;
            CompactSidebar = false;
        }
    }

    private static SplitNode CloneSplitNode(SplitNode source, Dictionary<string, string> paneIdMap)
    {
        var clone = new SplitNode
        {
            IsLeaf = source.IsLeaf,
            Direction = source.Direction,
            SplitRatio = source.SplitRatio,
        };

        if (source.IsLeaf)
        {
            var oldPaneId = source.PaneId;
            var newPaneId = Guid.NewGuid().ToString();
            clone.PaneId = newPaneId;

            if (!string.IsNullOrWhiteSpace(oldPaneId))
                paneIdMap[oldPaneId] = newPaneId;
        }
        else
        {
            clone.First = source.First != null ? CloneSplitNode(source.First, paneIdMap) : null;
            clone.Second = source.Second != null ? CloneSplitNode(source.Second, paneIdMap) : null;
        }

        return clone;
    }

    private async Task<string> HandlePipeCommand(string command, Dictionary<string, string> args)
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            return command switch
            {
                "NOTIFY" => HandleNotifyCommand(args),
                "WORKSPACE.LIST" => HandleWorkspaceList(),
                "WORKSPACE.CREATE" => HandleWorkspaceCreate(args),
                "WORKSPACE.SELECT" => HandleWorkspaceSelect(args),
                "SURFACE.CREATE" => HandleSurfaceCreate(args),
                "SURFACE.SELECT" => HandleSurfaceSelect(args),
                "SPLIT.RIGHT" => HandleSplit(SplitDirection.Vertical),
                "SPLIT.DOWN" => HandleSplit(SplitDirection.Horizontal),
                "PANE.LIST" => HandlePaneList(args),
                "PANE.FOCUS" => HandlePaneFocus(args),
                "PANE.WRITE" => HandlePaneWrite(args),
                "PANE.READ" => HandlePaneRead(args),
                "STATUS" => HandleStatus(),
                "STATUS.SET" => HandleStatusSet(args),
                "STATUS.CLEAR" => HandleStatusClear(args),
                "HOOKS" => HandleHookEvent(args),
                "EVENTS.STREAM" => "<<STREAM>>",
                _ => JsonSerializer.Serialize(new { error = $"Unknown command: {command}" }),
            };
        });
    }

    private async Task HandleEventStream(Action<string> writeLine, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource();
        using var reg = ct.Register(() => tcs.TrySetResult());

        var subId = EventBus.Subscribe(evt =>
        {
            try { writeLine(evt.ToJson()); }
            catch { tcs.TrySetResult(); }
        });

        try
        {
            await tcs.Task;
        }
        finally
        {
            EventBus.Unsubscribe(subId);
        }
    }

    private string HandleNotifyCommand(Dictionary<string, string> args)
    {
        var title = args.GetValueOrDefault("title", "Terminal");
        var body = args.GetValueOrDefault("body", "");
        var subtitle = args.GetValueOrDefault("subtitle");
        var workspaceId = SelectedWorkspace?.Workspace.Id ?? "";
        var surfaceId = SelectedWorkspace?.SelectedSurface?.Surface.Id ?? "";

        _notificationService.AddNotification(
            workspaceId, surfaceId, null,
            title, subtitle, body,
            NotificationSource.Cli);

        return JsonSerializer.Serialize(new { ok = true });
    }

    private string HandleWorkspaceList()
    {
        var list = Workspaces.Select(w => new
        {
            id = w.Workspace.Id,
            name = w.Workspace.Name,
            selected = w == SelectedWorkspace,
            surfaces = w.Surfaces.Count,
        });
        return JsonSerializer.Serialize(list);
    }

    private string HandleWorkspaceCreate(Dictionary<string, string> args)
    {
        CreateNewWorkspace();
        var ws = Workspaces[^1];
        if (args.TryGetValue("name", out var name))
            ws.Name = name;
        return JsonSerializer.Serialize(new { id = ws.Workspace.Id, name = ws.Name });
    }

    private string HandleWorkspaceSelect(Dictionary<string, string> args)
    {
        if (args.TryGetValue("index", out var indexStr) && int.TryParse(indexStr, out int index))
        {
            if (TryResolveCollectionIndex(index, Workspaces.Count, out var resolvedIndex))
            {
                SelectWorkspace(resolvedIndex);
                return JsonSerializer.Serialize(new { ok = true });
            }
        }
        if (args.TryGetValue("id", out var id))
        {
            var ws = Workspaces.FirstOrDefault(w => w.Workspace.Id == id);
            if (ws != null)
            {
                SelectedWorkspace = ws;
                return JsonSerializer.Serialize(new { ok = true });
            }
        }
        if (args.TryGetValue("name", out var name))
        {
            var ws = Workspaces.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? Workspaces.FirstOrDefault(w => w.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (ws != null)
            {
                SelectedWorkspace = ws;
                return JsonSerializer.Serialize(new { ok = true });
            }
        }
        return JsonSerializer.Serialize(new { error = "Workspace not found" });
    }

    private string HandleSurfaceCreate(Dictionary<string, string> args)
    {
        SelectedWorkspace?.CreateNewSurface();
        return JsonSerializer.Serialize(new { ok = true });
    }

    private string HandleSurfaceSelect(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        SelectedWorkspace = workspace;
        workspace.SelectedSurface = surface;

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
        });
    }

    private string HandleSplit(SplitDirection direction)
    {
        SelectedWorkspace?.SelectedSurface?.SplitFocused(direction);
        return JsonSerializer.Serialize(new { ok = true });
    }

    private string HandlePaneList(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        var leaves = surface.RootNode.GetLeaves()
            .Select(l => l.PaneId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();

        var panes = leaves
            .Select((paneId, idx) =>
            {
                surface.Surface.PaneCustomNames.TryGetValue(paneId, out var customName);
                return new
                {
                    index = idx + 1,
                    id = paneId,
                    name = string.IsNullOrWhiteSpace(customName) ? $"Pane {idx + 1}" : customName,
                    customName = customName ?? "",
                    focused = string.Equals(surface.FocusedPaneId, paneId, StringComparison.Ordinal),
                    workingDirectory = surface.GetSession(paneId)?.WorkingDirectory ?? "",
                };
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            workspace = new
            {
                id = workspace.Workspace.Id,
                name = workspace.Name,
            },
            surface = new
            {
                id = surface.Surface.Id,
                name = surface.Name,
            },
            panes,
        });
    }

    private string HandlePaneFocus(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out var paneIndex, out var paneName, out error))
            return JsonSerializer.Serialize(new { error });

        SelectedWorkspace = workspace;
        workspace.SelectedSurface = surface;
        surface.FocusPane(paneId);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            paneId,
            paneIndex,
            paneName,
        });
    }

    private string HandlePaneWrite(Dictionary<string, string> args)
    {
        var text = args.TryGetValue("text", out var requestedText) ? (requestedText ?? "") : "";

        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out var paneIndex, out var paneName, out error))
            return JsonSerializer.Serialize(new { error });

        var session = surface.GetSession(paneId);
        if (session == null)
            return JsonSerializer.Serialize(new { error = $"Pane session not found: {paneId}" });

        bool submit = args.TryGetValue("submit", out var submitRaw)
            && bool.TryParse(submitRaw, out var parsedSubmit)
            && parsedSubmit;

        if (submit)
            text = text.TrimEnd('\r', '\n');

        if (!submit && string.IsNullOrWhiteSpace(text))
            return JsonSerializer.Serialize(new { error = "Missing required argument: text" });

        var submitKey = args.TryGetValue("submitKey", out var submitKeyRaw)
            ? submitKeyRaw ?? ""
            : "auto";

        if (!string.IsNullOrEmpty(text))
            session.Write(text);

        if (submit)
        {
            var submitSequence = ResolveSubmitSequence(submitKey);
            if (!string.IsNullOrEmpty(submitSequence))
                session.Write(submitSequence);

            if (!string.IsNullOrWhiteSpace(text))
                surface.RegisterCommandSubmission(paneId, text);
        }

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            paneId,
            paneIndex,
            paneName,
            submit,
            submitKey,
            bytes = text.Length,
        });
    }

    private string HandlePaneRead(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out var paneIndex, out var paneName, out error))
            return JsonSerializer.Serialize(new { error });

        var session = surface.GetSession(paneId);
        if (session == null)
            return JsonSerializer.Serialize(new { error = $"Pane session not found: {paneId}" });

        int lines = 80;
        if (args.TryGetValue("lines", out var linesRaw) && int.TryParse(linesRaw, out var parsedLines))
            lines = Math.Clamp(parsedLines, 1, 5000);

        int maxChars = 20000;
        if (args.TryGetValue("maxChars", out var charsRaw) && int.TryParse(charsRaw, out var parsedChars))
            maxChars = Math.Clamp(parsedChars, 512, 200000);

        var allText = session.Buffer.ExportPlainText(maxScrollbackLines: 20000);
        var tailText = TailLines(allText, lines);
        if (tailText.Length > maxChars)
            tailText = "..." + tailText[^maxChars..];

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            paneId,
            paneIndex,
            paneName,
            lines,
            maxChars,
            text = tailText,
        });
    }

    private string HandleStatus()
    {
        return JsonSerializer.Serialize(new
        {
            version = "1.0.6",
            workspaces = Workspaces.Count,
            selectedWorkspace = SelectedWorkspace?.Workspace.Id,
            unreadNotifications = TotalUnreadCount,
        });
    }

    private string HandleStatusSet(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var ws, out var err)) return JsonSerializer.Serialize(new { error = err });
        var key = args.GetValueOrDefault("key", "");
        var value = args.GetValueOrDefault("value", "");
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            return JsonSerializer.Serialize(new { error = "key and value required" });

        ws.SetStatus(key, value,
            args.GetValueOrDefault("icon"),
            args.GetValueOrDefault("color"),
            int.TryParse(args.GetValueOrDefault("priority", "0"), out var p) ? p : 0);
        return JsonSerializer.Serialize(new { ok = true });
    }

    private string HandleStatusClear(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var ws, out var err)) return JsonSerializer.Serialize(new { error = err });
        ws.ClearStatus(args.GetValueOrDefault("key"));
        return JsonSerializer.Serialize(new { ok = true });
    }

    private string HandleHookEvent(Dictionary<string, string> args)
    {
        var json = args.GetValueOrDefault("payload", "{}");
        var evt = AgentHookService.ParseEvent(json);
        if (evt == null)
            return JsonSerializer.Serialize(new { error = "invalid hook payload" });

        var action = AgentHookService.ClassifyEvent(evt);

        var workspaceId = evt.WorkspaceId ?? SelectedWorkspace?.Workspace.Id ?? "";
        var surfaceId = evt.SurfaceId ?? SelectedWorkspace?.SelectedSurface?.Surface.Id ?? "";

        switch (action)
        {
            case HookAction.Notify:
            case HookAction.Approval:
                var title = action == HookAction.Approval
                    ? $"{evt.Agent} needs approval"
                    : evt.Title ?? $"{evt.Agent} — {evt.Event}";
                var body = action == HookAction.Approval
                    ? evt.Command ?? evt.Body ?? "Permission requested"
                    : evt.Body ?? "";
                _notificationService.AddNotification(
                    workspaceId, surfaceId, evt.PaneId,
                    title, evt.Tool, body,
                    NotificationSource.Cli);
                break;

            case HookAction.SessionStart:
                if (evt.SessionId != null)
                {
                    var ws = Workspaces.FirstOrDefault(w => w.Workspace.Id == workspaceId)
                        ?? SelectedWorkspace;
                    if (ws != null)
                    {
                        ws.AgentSessionId = evt.SessionId;
                        ws.AgentSessionAgent = evt.Agent;
                    }
                }
                break;
        }

        EventBus.Publish("agent.hook", new { agent = evt.Agent, @event = evt.Event, action = action.ToString() });

        return JsonSerializer.Serialize(new { ok = true, action = action.ToString() });
    }

    private bool TryResolveWorkspace(Dictionary<string, string> args, out WorkspaceViewModel workspace, out string error)
    {
        workspace = null!;
        error = "";

        var defaultWorkspace = SelectedWorkspace ?? Workspaces.FirstOrDefault();
        if (defaultWorkspace == null)
        {
            error = "No workspace available.";
            return false;
        }

        workspace = defaultWorkspace;

        if (args.TryGetValue("workspaceId", out var workspaceId) && !string.IsNullOrWhiteSpace(workspaceId))
        {
            var byId = Workspaces.FirstOrDefault(w => string.Equals(w.Workspace.Id, workspaceId, StringComparison.Ordinal));
            if (byId == null)
            {
                error = $"Workspace id not found: {workspaceId}";
                return false;
            }

            workspace = byId;
            return true;
        }

        if (args.TryGetValue("workspaceName", out var workspaceName) && !string.IsNullOrWhiteSpace(workspaceName))
        {
            var byName = Workspaces.FirstOrDefault(w => string.Equals(w.Name, workspaceName, StringComparison.OrdinalIgnoreCase))
                ?? Workspaces.FirstOrDefault(w => w.Name.Contains(workspaceName, StringComparison.OrdinalIgnoreCase));
            if (byName == null)
            {
                error = $"Workspace name not found: {workspaceName}";
                return false;
            }

            workspace = byName;
            return true;
        }

        if (args.TryGetValue("workspaceIndex", out var workspaceIndexRaw)
            && int.TryParse(workspaceIndexRaw, out var workspaceIndex))
        {
            if (!TryResolveCollectionIndex(workspaceIndex, Workspaces.Count, out var resolvedIndex))
            {
                error = $"Workspace index out of range: {workspaceIndex}";
                return false;
            }

            workspace = Workspaces[resolvedIndex];
            return true;
        }

        return true;
    }

    private static bool TryResolveSurface(WorkspaceViewModel workspace, Dictionary<string, string> args, out SurfaceViewModel surface, out string error)
    {
        surface = null!;
        error = "";

        var defaultSurface = workspace.SelectedSurface ?? workspace.Surfaces.FirstOrDefault();
        if (defaultSurface == null)
        {
            error = "No surface available in workspace.";
            return false;
        }

        surface = defaultSurface;

        if (args.TryGetValue("surfaceId", out var surfaceId) && !string.IsNullOrWhiteSpace(surfaceId))
        {
            var byId = workspace.Surfaces.FirstOrDefault(s => string.Equals(s.Surface.Id, surfaceId, StringComparison.Ordinal));
            if (byId == null)
            {
                error = $"Surface id not found: {surfaceId}";
                return false;
            }

            surface = byId;
            return true;
        }

        if (args.TryGetValue("surfaceName", out var surfaceName) && !string.IsNullOrWhiteSpace(surfaceName))
        {
            var byName = workspace.Surfaces.FirstOrDefault(s => string.Equals(s.Name, surfaceName, StringComparison.OrdinalIgnoreCase))
                ?? workspace.Surfaces.FirstOrDefault(s => s.Name.Contains(surfaceName, StringComparison.OrdinalIgnoreCase));
            if (byName == null)
            {
                error = $"Surface name not found: {surfaceName}";
                return false;
            }

            surface = byName;
            return true;
        }

        if (args.TryGetValue("surfaceIndex", out var surfaceIndexRaw)
            && int.TryParse(surfaceIndexRaw, out var surfaceIndex))
        {
            if (!TryResolveCollectionIndex(surfaceIndex, workspace.Surfaces.Count, out var resolvedIndex))
            {
                error = $"Surface index out of range: {surfaceIndex}";
                return false;
            }

            surface = workspace.Surfaces[resolvedIndex];
            return true;
        }

        return true;
    }

    private static bool TryResolvePaneId(
        SurfaceViewModel surface,
        Dictionary<string, string> args,
        out string paneId,
        out int paneIndex,
        out string paneName,
        out string error)
    {
        paneId = "";
        paneIndex = -1;
        paneName = "";
        error = "";

        var panes = surface.RootNode.GetLeaves()
            .Select(l => l.PaneId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Select((id, idx) =>
            {
                surface.Surface.PaneCustomNames.TryGetValue(id, out var customName);
                return new
                {
                    Id = id,
                    Index = idx + 1,
                    Name = string.IsNullOrWhiteSpace(customName) ? $"Pane {idx + 1}" : customName!,
                    CustomName = customName ?? "",
                };
            })
            .ToList();

        if (panes.Count == 0)
        {
            error = "No panes available in surface.";
            return false;
        }

        string? target = null;

        if (args.TryGetValue("paneId", out var requestedPaneId) && !string.IsNullOrWhiteSpace(requestedPaneId))
        {
            target = panes.FirstOrDefault(p => string.Equals(p.Id, requestedPaneId, StringComparison.Ordinal))?.Id;
            if (target == null)
            {
                error = $"Pane id not found: {requestedPaneId}";
                return false;
            }
        }
        else if (args.TryGetValue("paneName", out var requestedPaneName) && !string.IsNullOrWhiteSpace(requestedPaneName))
        {
            target = panes.FirstOrDefault(p => string.Equals(p.CustomName, requestedPaneName, StringComparison.OrdinalIgnoreCase))?.Id
                ?? panes.FirstOrDefault(p => string.Equals(p.Name, requestedPaneName, StringComparison.OrdinalIgnoreCase))?.Id;
            if (target == null)
            {
                error = $"Pane name not found: {requestedPaneName}";
                return false;
            }
        }
        else if (args.TryGetValue("paneIndex", out var paneIndexRaw) && int.TryParse(paneIndexRaw, out var requestedIndex))
        {
            if (!TryResolveCollectionIndex(requestedIndex, panes.Count, out var resolvedIndex))
            {
                error = $"Pane index out of range: {requestedIndex}";
                return false;
            }

            target = panes[resolvedIndex].Id;
        }
        else
        {
            target = !string.IsNullOrWhiteSpace(surface.FocusedPaneId)
                ? panes.FirstOrDefault(p => string.Equals(p.Id, surface.FocusedPaneId, StringComparison.Ordinal))?.Id
                : null;

            target ??= panes[0].Id;
        }

        var pane = panes.First(p => string.Equals(p.Id, target, StringComparison.Ordinal));
        paneId = pane.Id;
        paneIndex = pane.Index;
        paneName = pane.Name;
        return true;
    }

    private static bool TryResolveCollectionIndex(int requested, int count, out int zeroBasedIndex)
    {
        zeroBasedIndex = -1;
        if (count <= 0)
            return false;

        if (requested >= 1 && requested <= count)
        {
            zeroBasedIndex = requested - 1;
            return true;
        }

        if (requested >= 0 && requested < count)
        {
            zeroBasedIndex = requested;
            return true;
        }

        return false;
    }

    private static string TailLines(string? text, int lines)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var split = text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        var tail = split.TakeLast(Math.Max(1, lines));
        return string.Join("\n", tail).TrimEnd();
    }

    private static string ResolveSubmitSequence(string? submitKey)
    {
        var key = (submitKey ?? "auto").Trim().ToLowerInvariant();

        if (key is "auto" or "")
            return "\r";

        return key switch
        {
            "enter" or "cr" or "ctrl+m" => "\r",
            "linefeed" or "lf" or "ctrl+j" => "\n",
            "crlf" => "\r\n",
            "none" => "",
            _ => "\r",
        };
    }
}
