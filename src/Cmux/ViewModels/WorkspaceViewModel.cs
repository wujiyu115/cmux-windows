using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cmux.Core.Models;
using Cmux.Core.Services;
using Cmux.Services;

namespace Cmux.ViewModels;

public partial class WorkspaceViewModel : ObservableObject, IDisposable
{
    public Workspace Workspace { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _iconGlyph;

    [ObservableProperty]
    private string _accentColor;

    [ObservableProperty]
    private ObservableCollection<SurfaceViewModel> _surfaces = [];

    [ObservableProperty]
    private SurfaceViewModel? _selectedSurface;

    [ObservableProperty]
    private string? _gitBranch;

    [ObservableProperty]
    private string? _workingDirectory;

    [ObservableProperty]
    private string? _startDirectory;

    [ObservableProperty]
    private string? _latestNotificationText;

    [ObservableProperty]
    private int _unreadNotificationCount;

    [ObservableProperty]
    private string? _listeningPorts;

    [ObservableProperty]
    private bool _hasNotification;

    [ObservableProperty]
    private bool _isGitDirty;

    [ObservableProperty]
    private AgentType _detectedAgent;

    [ObservableProperty]
    private string? _agentSessionId;

    [ObservableProperty]
    private string? _agentSessionAgent;

    private string? _lastConfigPath;

    private readonly Dictionary<string, SidebarStatusEntry> _statusEntries = new();

    [ObservableProperty]
    private ObservableCollection<SidebarStatusEntry> _statusDisplay = new();

    public string AgentLabel => AgentDetector.GetLabel(DetectedAgent);
    public string AgentIcon => AgentDetector.GetIcon(DetectedAgent);
    public string IconFontFamily => IsPrivateUseGlyph(IconGlyph) ? "Segoe MDL2 Assets" : "Segoe UI Emoji";

    private readonly NotificationService _notificationService;
    private System.Threading.Timer? _infoRefreshTimer;

    public WorkspaceViewModel(Workspace workspace, NotificationService notificationService)
    {
        Workspace = workspace;
        _name = workspace.Name;
        _iconGlyph = workspace.IconGlyph;
        _accentColor = workspace.AccentColor;
        _startDirectory = workspace.StartDirectory;
        _notificationService = notificationService;

        // Create surface VMs for existing surfaces
        foreach (var surface in workspace.Surfaces)
        {
            var surfaceVm = new SurfaceViewModel(surface, workspace.Id, notificationService, workspaceStartDirectory: workspace.StartDirectory, workspaceEnvVars: workspace.EnvironmentVariables);
            surfaceVm.WorkingDirectoryChanged += OnSurfaceWorkingDirectoryChanged;
            Surfaces.Add(surfaceVm);
        }

        if (workspace.SelectedSurface != null)
        {
            SelectedSurface = Surfaces.FirstOrDefault(s => s.Surface.Id == workspace.SelectedSurface.Id);
        }
        else if (Surfaces.Count > 0)
        {
            SelectedSurface = Surfaces[0];
        }

        // Start periodic info refresh (git branch, ports)
        _infoRefreshTimer = new System.Threading.Timer(_ => RefreshInfo(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [RelayCommand]
    public void CreateNewSurface()
    {
        var surface = new Surface { Name = LanguageService.Lang("Default_Terminal", Surfaces.Count + 1) };
        Workspace.Surfaces.Add(surface);

        var surfaceVm = new SurfaceViewModel(surface, Workspace.Id, _notificationService, workspaceStartDirectory: Workspace.StartDirectory, workspaceEnvVars: Workspace.EnvironmentVariables);
        surfaceVm.WorkingDirectoryChanged += OnSurfaceWorkingDirectoryChanged;
        Surfaces.Add(surfaceVm);
        SelectedSurface = surfaceVm;
    }

    public void CreateNewSurfaceWithShell(string shellPath)
    {
        var surface = new Surface { Name = LanguageService.Lang("Default_Terminal", Surfaces.Count + 1) };
        Workspace.Surfaces.Add(surface);

        var surfaceVm = new SurfaceViewModel(surface, Workspace.Id, _notificationService, shellPath, workspaceStartDirectory: Workspace.StartDirectory, workspaceEnvVars: Workspace.EnvironmentVariables);
        surfaceVm.WorkingDirectoryChanged += OnSurfaceWorkingDirectoryChanged;
        Surfaces.Add(surfaceVm);
        SelectedSurface = surfaceVm;
    }

    [RelayCommand]
    public void CloseSurface(SurfaceViewModel? surface)
    {
        if (surface == null) return;
        if (Surfaces.Count <= 1) return; // Keep at least one

        int index = Surfaces.IndexOf(surface);
        surface.CaptureAllPaneTranscripts("surface-close");
        surface.WorkingDirectoryChanged -= OnSurfaceWorkingDirectoryChanged;
        surface.Dispose();
        Surfaces.Remove(surface);
        Workspace.Surfaces.Remove(surface.Surface);

        if (SelectedSurface == surface)
        {
            SelectedSurface = Surfaces[Math.Min(index, Surfaces.Count - 1)];
        }
    }

    [RelayCommand]
    public void NextSurface()
    {
        if (Surfaces.Count == 0) return;
        int index = SelectedSurface != null ? Surfaces.IndexOf(SelectedSurface) : -1;
        SelectedSurface = Surfaces[(index + 1) % Surfaces.Count];
    }

    [RelayCommand]
    public void PreviousSurface()
    {
        if (Surfaces.Count == 0) return;
        int index = SelectedSurface != null ? Surfaces.IndexOf(SelectedSurface) : 0;
        SelectedSurface = Surfaces[(index - 1 + Surfaces.Count) % Surfaces.Count];
    }

    [RelayCommand]
    public void Rename()
    {
        // This would be handled by the view showing an input box
    }

    private void OnSurfaceWorkingDirectoryChanged(string directory)
    {
        WorkingDirectory = directory;
        Workspace.WorkingDirectory = directory;
    }

    private void TryApplyProjectConfig(string? directory)
    {
        var configPath = ProjectConfigService.FindConfigPath(directory);
        if (configPath == _lastConfigPath) return;
        _lastConfigPath = configPath;

        if (configPath == null) return;
        var config = ProjectConfigService.LoadForDirectory(directory);
        if (config == null) return;

        if (config.Name != null && Name == Workspace.Id)
            Name = config.Name;
        if (config.Color != null)
            AccentColor = config.Color;
        if (config.Icon != null)
            IconGlyph = config.Icon;
        if (config.Env.Count > 0)
        {
            foreach (var (key, value) in config.Env)
                Workspace.EnvironmentVariables[key] = value;
        }
        if (config.StartDirectory != null)
            Workspace.StartDirectory = config.StartDirectory;
    }

    private void RefreshInfo()
    {
        try
        {
            var dir = WorkingDirectory ?? Workspace.WorkingDirectory;
            TryApplyProjectConfig(dir);
            if (!string.IsNullOrEmpty(dir))
            {
                var branch = GitService.GetBranch(dir);
                if (branch != GitBranch)
                {
                    GitBranch = branch;
                    Workspace.GitBranch = branch;
                }

                IsGitDirty = GitService.IsDirty(dir);
            }

            // AI agent detection
            var activeSurface = SelectedSurface;
            if (activeSurface?.ShellPid is int pid and > 0)
            {
                var agent = AgentDetector.DetectFromProcessId(pid);
                if (agent != DetectedAgent)
                {
                    DetectedAgent = agent;
                    OnPropertyChanged(nameof(AgentLabel));
                    OnPropertyChanged(nameof(AgentIcon));
                }

                if (agent != AgentType.None)
                {
                    var sessionId = AgentDetector.GetSessionId(agent, pid);
                    if (sessionId != null)
                    {
                        AgentSessionId = sessionId;
                        AgentSessionAgent = agent.ToString();
                    }
                }
            }

            // Port scanning
            try
            {
                if (activeSurface?.ShellPid is int shellPid and > 0)
                {
                    var ports = PortScanner.GetListeningPorts(shellPid);
                    ListeningPorts = ports.Count > 0 ? string.Join(", ", ports) : null;
                }
                else
                {
                    ListeningPorts = null;
                }
            }
            catch
            {
                ListeningPorts = null;
            }
        }
        catch
        {
            // Non-critical
        }
    }

    partial void OnUnreadNotificationCountChanged(int value)
    {
        HasNotification = value > 0;
    }

    partial void OnNameChanged(string value)
    {
        Workspace.Name = value;
    }

    partial void OnIconGlyphChanged(string value)
    {
        Workspace.IconGlyph = value;
        OnPropertyChanged(nameof(IconFontFamily));
    }

    partial void OnAccentColorChanged(string value)
    {
        Workspace.AccentColor = value;
    }

    partial void OnStartDirectoryChanged(string? value)
    {
        Workspace.StartDirectory = value;
    }

    private static bool IsPrivateUseGlyph(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        int codePoint = char.ConvertToUtf32(value, 0);
        return codePoint is >= 0xE000 and <= 0xF8FF;
    }

    public int CaptureAllSurfaceTranscripts(string reason)
    {
        int captured = 0;
        foreach (var surface in Surfaces)
            captured += surface.CaptureAllPaneTranscripts(reason);

        return captured;
    }

    public void SetStatus(string key, string value, string? icon = null, string? color = null, int priority = 0)
    {
        _statusEntries[key] = new SidebarStatusEntry { Key = key, Value = value, Icon = icon, Color = color, Priority = priority };
        RefreshStatusDisplay();
    }

    public void ClearStatus(string? key = null)
    {
        if (key != null)
            _statusEntries.Remove(key);
        else
            _statusEntries.Clear();
        RefreshStatusDisplay();
    }

    private void RefreshStatusDisplay()
    {
        StatusDisplay.Clear();
        foreach (var entry in _statusEntries.Values.OrderByDescending(e => e.Priority))
            StatusDisplay.Add(entry);
    }

    public void TryResumeAgentSession()
    {
        if (AgentSessionId == null || AgentSessionAgent == null) return;

        var resumeCmd = AgentSessionAgent.ToLowerInvariant() switch
        {
            "claudecode" => $"claude --resume {AgentSessionId}",
            "codex" => $"codex resume {AgentSessionId}",
            _ => null,
        };

        if (resumeCmd == null) return;

        // Write resume command to the focused pane after a delay for shell readiness
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            var paneId = SelectedSurface?.FocusedPaneId;
            if (paneId != null)
            {
                var session = SelectedSurface?.GetSession(paneId);
                session?.Write(resumeCmd + "\r");
            }
        });
    }

    public void Dispose()
    {
        _infoRefreshTimer?.Dispose();
        foreach (var surface in Surfaces)
        {
            surface.WorkingDirectoryChanged -= OnSurfaceWorkingDirectoryChanged;
            surface.Dispose();
        }
    }
}
