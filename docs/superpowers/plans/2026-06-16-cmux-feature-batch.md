# cmux-windows Feature Batch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 11 features to cmux-windows inspired by upstream cmux (macOS), covering sidebar enhancements, agent lifecycle, configuration, input, and IPC.

**Architecture:** Each feature is independent and can be implemented in any order. All new services go in `Cmux.Core` (for testability). UI code stays in `Cmux`. The codebase uses CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`), Named Pipe IPC (`COMMAND key=value` format), xUnit tests with FluentAssertions, and `System.Text.Json` for serialization.

**Tech Stack:** .NET 10, WPF, C#, CommunityToolkit.Mvvm, xUnit, FluentAssertions, System.Text.Json, ConPTY

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `src/Cmux.Core/Services/GitDirtyWatcher.cs` | FileSystemWatcher on `.git/` for dirty state detection |
| `src/Cmux.Core/Models/WorkspaceGroup.cs` | Workspace group model |
| `src/Cmux.Core/Models/SidebarStatusEntry.cs` | Sidebar status entry model |
| `src/Cmux.Core/Services/ProjectConfigService.cs` | Parse `.cmux/cmux.json` project config |
| `src/Cmux.Core/Models/ProjectConfig.cs` | Project config model |
| `src/Cmux.Core/Services/EventBus.cs` | App-wide event bus for event streaming |
| `src/Cmux.Core/Services/AgentHookService.cs` | Agent hook configuration and event processing |
| `src/Cmux.Core/Models/AgentHookEvent.cs` | Hook event model |
| `src/Cmux.Core/Services/FuzzyMatcher.cs` | Fuzzy search scoring algorithm |
| `src/Cmux/Input/ChordKeyHandler.cs` | Two-step chord key sequence handler |
| `src/Cmux/Controls/WorkspaceGroupHeader.xaml` | Collapsible group header UI |
| `src/Cmux/Controls/WorkspaceGroupHeader.xaml.cs` | Group header code-behind |
| `tests/Cmux.Tests/FuzzyMatcherTests.cs` | Fuzzy matcher tests |
| `tests/Cmux.Tests/GitDirtyWatcherTests.cs` | Git dirty state tests |
| `tests/Cmux.Tests/EventBusTests.cs` | Event bus tests |
| `tests/Cmux.Tests/ProjectConfigTests.cs` | Project config parsing tests |
| `tests/Cmux.Tests/AgentHookTests.cs` | Agent hook tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/Cmux.Core/Services/GitService.cs` | Add `IsDirty(string? dir)` method |
| `src/Cmux.Core/Models/Workspace.cs` | Add `EnvironmentVariables`, `GroupId` properties |
| `src/Cmux.Core/Models/SessionState.cs` | Add `EnvironmentVariables`, `GroupId`, `Groups`, `StatusEntries` to state models |
| `src/Cmux.Core/Config/CmuxSettings.cs` | Add `ChordTimeoutMs` setting |
| `src/Cmux.Core/Terminal/TerminalProcess.cs` | Inject workspace environment variables |
| `src/Cmux.Core/Services/PortScanner.cs` | (no changes, already complete) |
| `src/Cmux.Core/Services/AgentDetector.cs` | Add `GetSessionId` method per agent type |
| `src/Cmux/ViewModels/MainViewModel.cs` | Add groups, event bus, new IPC commands, agent hooks |
| `src/Cmux/ViewModels/WorkspaceViewModel.cs` | Add `IsGitDirty`, `ListeningPorts`, `StatusEntries`, `EnvironmentVariables`, `GroupId` |
| `src/Cmux/Controls/WorkspaceSidebarItem.xaml` | Add dirty indicator, ports row, status entries |
| `src/Cmux/Controls/CommandPalette.xaml.cs` | Replace substring filter with fuzzy match + char highlighting |
| `src/Cmux/Controls/CommandPalette.xaml` | Add TextBlock with Run elements for match highlighting |
| `src/Cmux/Views/MainWindow.xaml.cs` | Integrate ChordKeyHandler, add chord shortcut definitions |
| `src/Cmux/Views/SettingsWindow.xaml` | Add env var editor, group management |
| `src/Cmux.Cli/Program.cs` | Add `events`, `hooks` subcommands |
| `src/Cmux/Strings/Strings.en.xaml` | Add new string resources |
| `src/Cmux/Strings/Strings.zh.xaml` | Add new string resources (Chinese) |

---

## Task 1: Git Dirty State Detection

**Files:**
- Modify: `src/Cmux.Core/Services/GitService.cs`
- Modify: `src/Cmux/ViewModels/WorkspaceViewModel.cs`
- Modify: `src/Cmux/Controls/WorkspaceSidebarItem.xaml`
- Modify: `src/Cmux/Strings/Strings.en.xaml`
- Modify: `src/Cmux/Strings/Strings.zh.xaml`
- Test: `tests/Cmux.Tests/CoreTests.cs`

- [ ] **Step 1: Add `IsDirty` to GitService**

In `src/Cmux.Core/Services/GitService.cs`, add a new public static method after the existing `GetBranch` method:

```csharp
public static bool IsDirty(string? directory)
{
    if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        return false;
    try
    {
        var psi = new ProcessStartInfo("git", "status --porcelain")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi);
        if (proc == null) return false;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(3000);
        return !string.IsNullOrWhiteSpace(output);
    }
    catch
    {
        return false;
    }
}
```

- [ ] **Step 2: Add `IsGitDirty` property to WorkspaceViewModel**

In `src/Cmux/ViewModels/WorkspaceViewModel.cs`, add a new observable property near the existing `_gitBranch` field:

```csharp
[ObservableProperty]
private bool _isGitDirty;
```

In the existing `RefreshInfo()` method, after the line that sets `GitBranch`, add:

```csharp
IsGitDirty = GitService.IsDirty(dir);
```

- [ ] **Step 3: Add dirty indicator to sidebar XAML**

In `src/Cmux/Controls/WorkspaceSidebarItem.xaml`, find the git branch TextBlock (the one with `{Binding GitBranch}`). Wrap it in a StackPanel with a dirty dot:

```xml
<!-- Replace the existing git branch TextBlock with: -->
<StackPanel Orientation="Horizontal" Grid.Row="2"
            Visibility="{Binding GitBranch, Converter={StaticResource NullToCollapsed}}">
    <TextBlock Text="&#xE8AD;" FontFamily="Segoe MDL2 Assets" FontSize="10"
               Foreground="{DynamicResource ForegroundDimBrush}" Margin="0,0,4,0"
               VerticalAlignment="Center"/>
    <TextBlock Text="{Binding GitBranch}" FontSize="11"
               Foreground="{DynamicResource ForegroundDimBrush}"
               VerticalAlignment="Center"/>
    <Ellipse Width="6" Height="6" Margin="5,0,0,0"
             Fill="{DynamicResource WarningBrush}"
             VerticalAlignment="Center"
             Visibility="{Binding IsGitDirty, Converter={StaticResource BoolToVisibility}}"/>
</StackPanel>
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build src/Cmux/Cmux.csproj`
Expected: Build succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add src/Cmux.Core/Services/GitService.cs src/Cmux/ViewModels/WorkspaceViewModel.cs src/Cmux/Controls/WorkspaceSidebarItem.xaml
git commit -m "feat: add git dirty state indicator in workspace sidebar"
```

---

## Task 2: Port Display in Sidebar

**Files:**
- Modify: `src/Cmux/ViewModels/WorkspaceViewModel.cs`
- Modify: `src/Cmux/Controls/WorkspaceSidebarItem.xaml`

- [ ] **Step 1: Add `ListeningPorts` property to WorkspaceViewModel**

In `src/Cmux/ViewModels/WorkspaceViewModel.cs`, add:

```csharp
[ObservableProperty]
private string? _listeningPorts;
```

In the existing `RefreshInfo()` method, after the agent detection block, add:

```csharp
try
{
    var focusedSession = SelectedSurface?.GetFocusedSession();
    if (focusedSession?.ShellProcessId is int pid and > 0)
    {
        var ports = PortScanner.GetListeningPorts(pid);
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
```

Add the using at the top: `using Cmux.Core.Services;` (if not already present).

- [ ] **Step 2: Add ports row to sidebar XAML**

In `src/Cmux/Controls/WorkspaceSidebarItem.xaml`, add a new row after the working directory row (after the existing Row 3 content). First, add a new `RowDefinition` with `Height="Auto"` and shift subsequent rows. Then add:

```xml
<StackPanel Orientation="Horizontal" Grid.Row="4"
            Visibility="{Binding ListeningPorts, Converter={StaticResource NullToCollapsed}}"
            Margin="0,1,0,0">
    <TextBlock Text="&#xE968;" FontFamily="Segoe MDL2 Assets" FontSize="10"
               Foreground="{DynamicResource TealAccent}" Margin="0,0,4,0"
               VerticalAlignment="Center" ToolTip="Listening ports"/>
    <TextBlock Text="{Binding ListeningPorts}" FontSize="10"
               Foreground="{DynamicResource ForegroundDimBrush}"
               TextTrimming="CharacterEllipsis" MaxWidth="140"
               VerticalAlignment="Center"/>
</StackPanel>
```

Adjust the remaining rows (notification text row) to use the next row index.

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/Cmux/Cmux.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Cmux/ViewModels/WorkspaceViewModel.cs src/Cmux/Controls/WorkspaceSidebarItem.xaml
git commit -m "feat: display listening ports in workspace sidebar"
```

---

## Task 3: Workspace Environment Variables

**Files:**
- Modify: `src/Cmux.Core/Models/Workspace.cs`
- Modify: `src/Cmux.Core/Models/SessionState.cs`
- Modify: `src/Cmux.Core/Terminal/TerminalProcess.cs`
- Modify: `src/Cmux/ViewModels/WorkspaceViewModel.cs`
- Modify: `src/Cmux/ViewModels/SurfaceViewModel.cs`
- Modify: `src/Cmux/Views/SettingsWindow.xaml`
- Modify: `src/Cmux/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: Add `EnvironmentVariables` to Workspace model**

In `src/Cmux.Core/Models/Workspace.cs`, add:

```csharp
public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
```

- [ ] **Step 2: Add to SessionState for persistence**

In `src/Cmux.Core/Models/SessionState.cs`, in the `WorkspaceState` class, add:

```csharp
[JsonPropertyName("environmentVariables")]
public Dictionary<string, string>? EnvironmentVariables { get; set; }
```

Update the save logic in `MainViewModel.SaveSession` (or wherever `WorkspaceState` is built) to include:

```csharp
EnvironmentVariables = ws.Workspace.EnvironmentVariables.Count > 0
    ? ws.Workspace.EnvironmentVariables : null,
```

Update the restore logic to read it back:

```csharp
workspace.EnvironmentVariables = wsState.EnvironmentVariables ?? new();
```

- [ ] **Step 3: Inject env vars into terminal process creation**

In `src/Cmux/ViewModels/SurfaceViewModel.cs`, find the `StartSession` or `StartLocalSession` method where `TerminalSession` is created. The environment variables need to flow from `WorkspaceViewModel` down to the terminal.

Add a property to `SurfaceViewModel`:

```csharp
public Dictionary<string, string>? WorkspaceEnvironmentVariables { get; set; }
```

Set it when creating surfaces in `WorkspaceViewModel.CreateNewSurface`:

```csharp
surfaceVm.WorkspaceEnvironmentVariables = Workspace.EnvironmentVariables;
```

In `TerminalProcess.cs`, modify the constructor or the `Start` method. Find where `CreateProcess` is called and before that, inject environment variables:

```csharp
if (environmentVariables?.Count > 0)
{
    foreach (var (key, value) in environmentVariables)
        startInfo.Environment[key] = value;
}
```

The `TerminalProcess` constructor needs a new optional parameter `Dictionary<string, string>? environmentVariables = null`, which flows through `TerminalSession.Start` and `SurfaceViewModel.StartSession`.

- [ ] **Step 4: Add env var editor in workspace context menu**

In `src/Cmux/Controls/WorkspaceSidebarItem.xaml`, add a context menu item:

```xml
<MenuItem Header="{DynamicResource Workspace_EnvVars}" Click="EnvVars_Click"/>
```

In `WorkspaceSidebarItem.xaml.cs`, add:

```csharp
private void EnvVars_Click(object sender, RoutedEventArgs e)
{
    if (DataContext is not WorkspaceViewModel vm) return;
    var window = new TextPromptWindow(
        LanguageService.Lang("Workspace_EnvVars"),
        FormatEnvVars(vm.Workspace.EnvironmentVariables));
    window.Owner = Window.GetWindow(this);
    if (window.ShowDialog() == true)
    {
        vm.Workspace.EnvironmentVariables = ParseEnvVars(window.ResultText);
    }
}

private static string FormatEnvVars(Dictionary<string, string> vars)
    => string.Join("\n", vars.Select(kv => $"{kv.Key}={kv.Value}"));

private static Dictionary<string, string> ParseEnvVars(string text)
{
    var dict = new Dictionary<string, string>();
    foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        var eq = line.IndexOf('=');
        if (eq > 0)
            dict[line[..eq].Trim()] = line[(eq + 1)..].Trim();
    }
    return dict;
}
```

- [ ] **Step 5: Add i18n strings**

In `Strings.en.xaml`:
```xml
<system:String x:Key="Workspace_EnvVars">Environment Variables</system:String>
```

In `Strings.zh.xaml`:
```xml
<system:String x:Key="Workspace_EnvVars">环境变量</system:String>
```

- [ ] **Step 6: Build and verify**

Run: `dotnet build src/Cmux/Cmux.csproj`
Expected: Build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/Cmux.Core/Models/Workspace.cs src/Cmux.Core/Models/SessionState.cs src/Cmux.Core/Terminal/TerminalProcess.cs src/Cmux/ViewModels/WorkspaceViewModel.cs src/Cmux/ViewModels/SurfaceViewModel.cs src/Cmux/Controls/WorkspaceSidebarItem.xaml src/Cmux/Controls/WorkspaceSidebarItem.xaml.cs src/Cmux/Strings/Strings.en.xaml src/Cmux/Strings/Strings.zh.xaml
git commit -m "feat: add per-workspace environment variables"
```

---

## Task 4: Enhanced Command Palette — Fuzzy Search

**Files:**
- Create: `src/Cmux.Core/Services/FuzzyMatcher.cs`
- Modify: `src/Cmux/Controls/CommandPalette.xaml.cs`
- Modify: `src/Cmux/Controls/CommandPalette.xaml`
- Test: `tests/Cmux.Tests/FuzzyMatcherTests.cs`

- [ ] **Step 1: Write fuzzy matcher tests**

Create `tests/Cmux.Tests/FuzzyMatcherTests.cs`:

```csharp
using Cmux.Core.Services;
using FluentAssertions;

namespace Cmux.Tests;

public class FuzzyMatcherTests
{
    [Fact]
    public void Score_ExactMatch_ReturnsHighScore()
    {
        var result = FuzzyMatcher.Score("settings", "settings");
        result.Score.Should().BeGreaterThan(100);
        result.MatchedIndices.Should().HaveCount(8);
    }

    [Fact]
    public void Score_PrefixMatch_ScoresHigherThanMiddleMatch()
    {
        var prefix = FuzzyMatcher.Score("set", "settings");
        var middle = FuzzyMatcher.Score("set", "reset");
        prefix.Score.Should().BeGreaterThan(middle.Score);
    }

    [Fact]
    public void Score_SubsequenceMatch_ReturnsPositiveScore()
    {
        var result = FuzzyMatcher.Score("nw", "new workspace");
        result.Score.Should().BeGreaterThan(0);
        result.MatchedIndices.Should().Contain(0); // 'n' in 'new'
        result.MatchedIndices.Should().Contain(4); // 'w' in 'workspace'
    }

    [Fact]
    public void Score_NoMatch_ReturnsZero()
    {
        var result = FuzzyMatcher.Score("xyz", "settings");
        result.Score.Should().Be(0);
        result.MatchedIndices.Should().BeEmpty();
    }

    [Fact]
    public void Score_WordBoundaryMatch_ScoresHigherThanMidWordMatch()
    {
        var boundary = FuzzyMatcher.Score("cs", "close surface");
        var midWord = FuzzyMatcher.Score("cs", "access");
        boundary.Score.Should().BeGreaterThan(midWord.Score);
    }

    [Fact]
    public void Score_ConsecutiveMatch_ScoresHigherThanScattered()
    {
        var consecutive = FuzzyMatcher.Score("new", "new workspace");
        var scattered = FuzzyMatcher.Score("new", "notification preview");
        consecutive.Score.Should().BeGreaterThan(scattered.Score);
    }

    [Fact]
    public void Score_CaseInsensitive()
    {
        var lower = FuzzyMatcher.Score("set", "Settings");
        lower.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Score_EmptyPattern_ReturnsZero()
    {
        var result = FuzzyMatcher.Score("", "settings");
        result.Score.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cmux.Tests/ --filter "FullyQualifiedName~FuzzyMatcherTests" -v n`
Expected: Build error — `FuzzyMatcher` does not exist.

- [ ] **Step 3: Implement FuzzyMatcher**

Create `src/Cmux.Core/Services/FuzzyMatcher.cs`:

```csharp
namespace Cmux.Core.Services;

public static class FuzzyMatcher
{
    public readonly record struct MatchResult(int Score, List<int> MatchedIndices);

    private static readonly MatchResult NoMatch = new(0, []);

    public static MatchResult Score(string pattern, string text)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(text))
            return NoMatch;

        var patternLower = pattern.ToLowerInvariant();
        var textLower = text.ToLowerInvariant();

        if (!CanMatch(patternLower, textLower))
            return NoMatch;

        var indices = new List<int>();
        var score = ScoreRecursive(patternLower, textLower, 0, 0, indices, text);
        if (score <= 0)
            return NoMatch;

        return new MatchResult(score, indices);
    }

    private static bool CanMatch(string pattern, string text)
    {
        int ti = 0;
        for (int pi = 0; pi < pattern.Length; pi++)
        {
            ti = text.IndexOf(pattern[pi], ti);
            if (ti < 0) return false;
            ti++;
        }
        return true;
    }

    private static int ScoreRecursive(string pattern, string textLower, int pi, int ti,
        List<int> bestIndices, string originalText)
    {
        if (pi == pattern.Length) return 0;

        int bestScore = -1;
        List<int>? bestSub = null;

        for (int i = ti; i < textLower.Length; i++)
        {
            if (textLower[i] != pattern[pi]) continue;

            var subIndices = new List<int>();
            int subScore = ScoreRecursive(pattern, textLower, pi + 1, i + 1, subIndices, originalText);
            if (subScore < 0 && pi + 1 < pattern.Length) continue;

            int charScore = 1;

            if (i == 0 || originalText[i] != textLower[i])
                charScore += 10;

            if (i > 0 && (originalText[i - 1] == ' ' || originalText[i - 1] == '_' || originalText[i - 1] == '-'))
                charScore += 8;

            if (pi > 0 && bestIndices.Count > 0 || subIndices.Count > 0)
            {
                var prevIdx = pi == 0 ? -1 :
                    (bestSub != null && bestSub.Count >= pi ? bestSub[pi - 1] : -1);
            }

            if (i == ti && pi > 0)
                charScore += 5;

            int total = charScore + Math.Max(0, subScore);
            if (total > bestScore)
            {
                bestScore = total;
                bestSub = [i, .. subIndices];
            }
        }

        if (bestSub != null)
        {
            bestIndices.Clear();
            bestIndices.AddRange(bestSub);
        }

        return bestScore;
    }

    public static MatchResult ScoreMultiField(string pattern, params string?[] fields)
    {
        var best = NoMatch;
        foreach (var field in fields)
        {
            if (string.IsNullOrEmpty(field)) continue;
            var result = Score(pattern, field);
            if (result.Score > best.Score)
                best = result;
        }
        return best;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cmux.Tests/ --filter "FullyQualifiedName~FuzzyMatcherTests" -v n`
Expected: All 8 tests pass.

- [ ] **Step 5: Integrate fuzzy matcher into CommandPalette**

In `src/Cmux/Controls/CommandPalette.xaml.cs`, find the `FilterItems` or equivalent method that does the substring filtering. Replace the filtering logic:

```csharp
private void FilterItems(string query)
{
    _filteredItems.Clear();
    if (string.IsNullOrWhiteSpace(query))
    {
        foreach (var item in _allItems.Take(20))
            _filteredItems.Add(new ScoredPaletteItem(item, 0, []));
    }
    else
    {
        var scored = _allItems
            .Select(item =>
            {
                var result = FuzzyMatcher.ScoreMultiField(query, item.Label, item.Description, item.Category);
                return new ScoredPaletteItem(item, result.Score, result.MatchedIndices);
            })
            .Where(s => s.Score > 0)
            .OrderByDescending(s => s.Score)
            .Take(20);

        foreach (var item in scored)
            _filteredItems.Add(item);
    }

    if (_filteredItems.Count > 0)
        SelectedIndex = 0;
}
```

Add a wrapper record:

```csharp
public record ScoredPaletteItem(PaletteItem Item, int Score, List<int> MatchedIndices);
```

Update the XAML ItemTemplate to highlight matched characters using a converter or code-behind that generates `Run` elements with accent foreground for matched indices.

- [ ] **Step 6: Build and verify**

Run: `dotnet build src/Cmux/Cmux.csproj`
Expected: Build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/Cmux.Core/Services/FuzzyMatcher.cs tests/Cmux.Tests/FuzzyMatcherTests.cs src/Cmux/Controls/CommandPalette.xaml.cs src/Cmux/Controls/CommandPalette.xaml
git commit -m "feat: upgrade command palette to fuzzy search with match highlighting"
```

---

## Task 5: Chord Shortcuts

**Files:**
- Create: `src/Cmux/Input/ChordKeyHandler.cs`
- Modify: `src/Cmux/Views/MainWindow.xaml.cs`
- Modify: `src/Cmux.Core/Config/CmuxSettings.cs`

- [ ] **Step 1: Create ChordKeyHandler**

Create `src/Cmux/Input/ChordKeyHandler.cs`:

```csharp
using System.Windows.Input;
using System.Windows.Threading;

namespace Cmux.Input;

public sealed class ChordKeyHandler
{
    private readonly Dictionary<(ModifierKeys, Key), Dictionary<(ModifierKeys, Key), Action>> _chords = new();
    private (ModifierKeys, Key)? _pendingFirst;
    private DispatcherTimer? _timer;
    private readonly int _timeoutMs;

    public bool IsWaitingForSecondKey => _pendingFirst.HasValue;
    public event Action<string>? ChordHintChanged;

    public ChordKeyHandler(int timeoutMs = 500)
    {
        _timeoutMs = timeoutMs;
    }

    public void Register(ModifierKeys mod1, Key key1, ModifierKeys mod2, Key key2, Action action)
    {
        var first = (mod1, key1);
        if (!_chords.TryGetValue(first, out var seconds))
        {
            seconds = new Dictionary<(ModifierKeys, Key), Action>();
            _chords[first] = seconds;
        }
        seconds[(mod2, key2)] = action;
    }

    public bool HandleKeyDown(ModifierKeys modifiers, Key key)
    {
        var combo = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt), key);

        if (_pendingFirst.HasValue)
        {
            CancelTimer();
            if (_chords.TryGetValue(_pendingFirst.Value, out var seconds) &&
                seconds.TryGetValue(combo, out var action))
            {
                _pendingFirst = null;
                ChordHintChanged?.Invoke("");
                action();
                return true;
            }
            _pendingFirst = null;
            ChordHintChanged?.Invoke("");
            return false;
        }

        if (_chords.ContainsKey(combo))
        {
            _pendingFirst = combo;
            ChordHintChanged?.Invoke(FormatCombo(combo) + ", ...");
            StartTimer();
            return true;
        }

        return false;
    }

    private void StartTimer()
    {
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_timeoutMs) };
        _timer.Tick += (_, _) =>
        {
            _pendingFirst = null;
            _timer.Stop();
            ChordHintChanged?.Invoke("");
        };
        _timer.Start();
    }

    private void CancelTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private static string FormatCombo((ModifierKeys mod, Key key) combo)
    {
        var parts = new List<string>();
        if (combo.mod.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (combo.mod.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (combo.mod.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        parts.Add(combo.key.ToString());
        return string.Join("+", parts);
    }
}
```

- [ ] **Step 2: Add `ChordTimeoutMs` setting**

In `src/Cmux.Core/Config/CmuxSettings.cs`, add:

```csharp
public int ChordTimeoutMs { get; set; } = 500;
```

- [ ] **Step 3: Integrate into MainWindow**

In `src/Cmux/Views/MainWindow.xaml.cs`, add a field:

```csharp
private ChordKeyHandler _chordHandler = null!;
```

In the constructor or `OnLoaded`, initialize it and register chord shortcuts:

```csharp
_chordHandler = new ChordKeyHandler(SettingsService.Current.ChordTimeoutMs);

// Ctrl+K, Ctrl+T — new surface (example chord)
_chordHandler.Register(ModifierKeys.Control, Key.K, ModifierKeys.Control, Key.T,
    () => ViewModel?.SelectedWorkspace?.CreateNewSurface());

// Ctrl+K, Ctrl+W — close workspace
_chordHandler.Register(ModifierKeys.Control, Key.K, ModifierKeys.Control, Key.W,
    () => ViewModel?.CloseWorkspace());

// Ctrl+K, Ctrl+D — split down
_chordHandler.Register(ModifierKeys.Control, Key.K, ModifierKeys.Control, Key.D,
    () => ViewModel?.SelectedWorkspace?.SelectedSurface?.SplitFocusedPane(SplitDirection.Horizontal));

// Ctrl+K, Ctrl+R — split right
_chordHandler.Register(ModifierKeys.Control, Key.K, ModifierKeys.Control, Key.R,
    () => ViewModel?.SelectedWorkspace?.SelectedSurface?.SplitFocusedPane(SplitDirection.Vertical));

_chordHandler.ChordHintChanged += hint =>
{
    // Show chord hint in status bar or title bar
    Title = string.IsNullOrEmpty(hint) ? "cmux" : $"cmux — {hint}";
};
```

In `OnKeyDown`, add as the **first check** before any other shortcut processing:

```csharp
if (_chordHandler.HandleKeyDown(Keyboard.Modifiers, e.Key))
{
    e.Handled = true;
    return;
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build src/Cmux/Cmux.csproj`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Cmux/Input/ChordKeyHandler.cs src/Cmux.Core/Config/CmuxSettings.cs src/Cmux/Views/MainWindow.xaml.cs
git commit -m "feat: add chord keyboard shortcuts (Ctrl+K, Ctrl+X sequences)"
```

---

## Task 6: Sidebar Status Entries

**Files:**
- Create: `src/Cmux.Core/Models/SidebarStatusEntry.cs`
- Modify: `src/Cmux/ViewModels/WorkspaceViewModel.cs`
- Modify: `src/Cmux/ViewModels/MainViewModel.cs`
- Modify: `src/Cmux/Controls/WorkspaceSidebarItem.xaml`
- Modify: `src/Cmux.Core/Models/SessionState.cs`
- Modify: `src/Cmux.Cli/Program.cs`

- [ ] **Step 1: Create SidebarStatusEntry model**

Create `src/Cmux.Core/Models/SidebarStatusEntry.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Cmux.Core.Models;

public class SidebarStatusEntry
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}
```

- [ ] **Step 2: Add StatusEntries to WorkspaceViewModel**

In `src/Cmux/ViewModels/WorkspaceViewModel.cs`, add:

```csharp
private readonly Dictionary<string, SidebarStatusEntry> _statusEntries = new();

[ObservableProperty]
private ObservableCollection<SidebarStatusEntry> _statusDisplay = new();

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
```

- [ ] **Step 3: Add IPC commands**

In `src/Cmux/ViewModels/MainViewModel.cs`, in `HandlePipeCommand`, add cases:

```csharp
"STATUS.SET" => HandleStatusSet(args),
"STATUS.CLEAR" => HandleStatusClear(args),
```

Implement the handlers:

```csharp
private string HandleStatusSet(Dictionary<string, string> args)
{
    if (!TryResolveWorkspace(args, out var ws, out var err)) return err;
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
    if (!TryResolveWorkspace(args, out var ws, out var err)) return err;
    ws.ClearStatus(args.GetValueOrDefault("key"));
    return JsonSerializer.Serialize(new { ok = true });
}
```

- [ ] **Step 4: Add status display to sidebar XAML**

In `src/Cmux/Controls/WorkspaceSidebarItem.xaml`, add a new row with an ItemsControl for status entries:

```xml
<ItemsControl Grid.Row="5" ItemsSource="{Binding StatusDisplay}"
              Visibility="{Binding StatusDisplay.Count, Converter={StaticResource CountToVisibility}}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal" Margin="0,1,0,0">
                <TextBlock Text="{Binding Icon}" FontFamily="Segoe MDL2 Assets" FontSize="10"
                           Foreground="{Binding Color, Converter={StaticResource HexToBrush}, FallbackValue={DynamicResource ForegroundDimBrush}}"
                           Margin="0,0,4,0" VerticalAlignment="Center"
                           Visibility="{Binding Icon, Converter={StaticResource NullToCollapsed}}"/>
                <TextBlock Text="{Binding Value}" FontSize="10"
                           Foreground="{DynamicResource ForegroundDimBrush}"
                           TextTrimming="CharacterEllipsis" MaxWidth="140"/>
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

You may need to add a `CountToVisibility` converter. If it doesn't exist, add one to `ValueConverters.cs`:

```csharp
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 5: Add CLI subcommand**

In `src/Cmux.Cli/Program.cs`, add a `status` subcommand handler:

```csharp
case "status":
    return await HandleStatus(subArgs);
```

```csharp
static async Task<int> HandleStatus(string[] args)
{
    if (args.Length < 1) { PrintUsage("status set|clear"); return 1; }
    return args[0] switch
    {
        "set" => await SendAndPrint("STATUS.SET", ParseKeyValues(args[1..])),
        "clear" => await SendAndPrint("STATUS.CLEAR", ParseKeyValues(args[1..])),
        _ => PrintUsage("status set|clear"),
    };
}
```

- [ ] **Step 6: Build and verify**

Run: `dotnet build`
Expected: All projects build.

- [ ] **Step 7: Commit**

```bash
git add src/Cmux.Core/Models/SidebarStatusEntry.cs src/Cmux/ViewModels/WorkspaceViewModel.cs src/Cmux/ViewModels/MainViewModel.cs src/Cmux/Controls/WorkspaceSidebarItem.xaml src/Cmux.Cli/Program.cs
git commit -m "feat: add sidebar status entries with CLI and IPC support"
```

---

## Task 7: Event Bus and Event Stream

**Files:**
- Create: `src/Cmux.Core/Services/EventBus.cs`
- Modify: `src/Cmux/ViewModels/MainViewModel.cs`
- Modify: `src/Cmux.Core/IPC/NamedPipeServer.cs`
- Modify: `src/Cmux.Cli/Program.cs`
- Test: `tests/Cmux.Tests/EventBusTests.cs`

- [ ] **Step 1: Write EventBus tests**

Create `tests/Cmux.Tests/EventBusTests.cs`:

```csharp
using Cmux.Core.Services;
using FluentAssertions;

namespace Cmux.Tests;

public class EventBusTests
{
    [Fact]
    public void Publish_NotifiesSubscribers()
    {
        var bus = new EventBus();
        var received = new List<AppEvent>();
        bus.Subscribe(e => received.Add(e));

        bus.Publish("workspace.created", new { name = "test" });

        received.Should().HaveCount(1);
        received[0].Type.Should().Be("workspace.created");
    }

    [Fact]
    public void Unsubscribe_StopsNotifications()
    {
        var bus = new EventBus();
        var received = new List<AppEvent>();
        var id = bus.Subscribe(e => received.Add(e));

        bus.Publish("test", null);
        bus.Unsubscribe(id);
        bus.Publish("test", null);

        received.Should().HaveCount(1);
    }

    [Fact]
    public void Publish_IncludesTimestamp()
    {
        var bus = new EventBus();
        AppEvent? captured = null;
        bus.Subscribe(e => captured = e);

        bus.Publish("test", null);

        captured.Should().NotBeNull();
        captured!.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cmux.Tests/ --filter "FullyQualifiedName~EventBusTests" -v n`
Expected: Build error — `EventBus` and `AppEvent` do not exist.

- [ ] **Step 3: Implement EventBus**

Create `src/Cmux.Core/Services/EventBus.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cmux.Core.Services;

public class AppEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("data")]
    public object? Data { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
}

public sealed class EventBus
{
    private readonly Dictionary<Guid, Action<AppEvent>> _subscribers = new();
    private readonly object _lock = new();

    public Guid Subscribe(Action<AppEvent> handler)
    {
        var id = Guid.NewGuid();
        lock (_lock) _subscribers[id] = handler;
        return id;
    }

    public void Unsubscribe(Guid id)
    {
        lock (_lock) _subscribers.Remove(id);
    }

    public void Publish(string type, object? data)
    {
        var evt = new AppEvent { Type = type, Data = data };
        Action<AppEvent>[] handlers;
        lock (_lock) handlers = _subscribers.Values.ToArray();
        foreach (var handler in handlers)
        {
            try { handler(evt); }
            catch { /* subscriber errors don't propagate */ }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cmux.Tests/ --filter "FullyQualifiedName~EventBusTests" -v n`
Expected: All 3 tests pass.

- [ ] **Step 5: Wire EventBus into MainViewModel**

In `src/Cmux/ViewModels/MainViewModel.cs`, add a public `EventBus` property:

```csharp
public EventBus EventBus { get; } = new();
```

Add event publishing calls at key points. For example, in `CreateNewWorkspace`:

```csharp
EventBus.Publish("workspace.created", new { id = ws.Workspace.Id, name = ws.Name });
```

In `CloseWorkspace`:

```csharp
EventBus.Publish("workspace.closed", new { id = ws.Workspace.Id, name = ws.Name });
```

In `OnSelectedWorkspaceChanged`:

```csharp
EventBus.Publish("workspace.selected", new { id = value?.Workspace.Id, name = value?.Name });
```

Similarly for surface and pane events. Wire notification events:

```csharp
EventBus.Publish("notification.added", new { title, body, workspaceId });
```

- [ ] **Step 6: Add IPC streaming endpoint**

In `src/Cmux.Core/IPC/NamedPipeServer.cs`, add a new property for a streaming handler:

```csharp
public Func<StreamWriter, CancellationToken, Task>? OnStreamRequest { get; set; }
```

In `HandlePipeCommand`, add a case for `EVENTS.STREAM`. Instead of returning a single line, this keeps the pipe open and writes events line by line:

In `MainViewModel.HandlePipeCommand`, add:

```csharp
"EVENTS.STREAM" => "STREAM",
```

Then in the NamedPipeServer, when the response is `"STREAM"`, switch to streaming mode: keep the pipe open and forward events from the EventBus until the client disconnects.

This requires modifying the server loop to detect the `STREAM` response and enter a different code path. A simpler approach is to add a separate `EVENTS.STREAM` handler directly in the pipe server that subscribes to the EventBus:

```csharp
if (command == "EVENTS.STREAM")
{
    var subId = eventBus.Subscribe(evt =>
    {
        try { writer.WriteLine(evt.ToJson()); writer.Flush(); }
        catch { /* client disconnected */ }
    });
    try
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
    finally
    {
        eventBus.Unsubscribe(subId);
    }
    return;
}
```

- [ ] **Step 7: Add CLI `events` command**

In `src/Cmux.Cli/Program.cs`, add:

```csharp
case "events":
    return await HandleEvents();
```

```csharp
static async Task<int> HandleEvents()
{
    var pipeName = ResolvePipeName();
    await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
    await pipe.ConnectAsync(3000);
    var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
    var reader = new StreamReader(pipe, new UTF8Encoding(false));
    await writer.WriteLineAsync("EVENTS.STREAM");
    while (true)
    {
        var line = await reader.ReadLineAsync();
        if (line == null) break;
        Console.WriteLine(line);
    }
    return 0;
}
```

- [ ] **Step 8: Build and verify**

Run: `dotnet build`
Expected: All projects build.

- [ ] **Step 9: Commit**

```bash
git add src/Cmux.Core/Services/EventBus.cs tests/Cmux.Tests/EventBusTests.cs src/Cmux/ViewModels/MainViewModel.cs src/Cmux.Core/IPC/NamedPipeServer.cs src/Cmux.Cli/Program.cs
git commit -m "feat: add event bus with CLI streaming endpoint (cmux events)"
```

---

## Task 8: Project-Level Config File

**Files:**
- Create: `src/Cmux.Core/Models/ProjectConfig.cs`
- Create: `src/Cmux.Core/Services/ProjectConfigService.cs`
- Modify: `src/Cmux/ViewModels/WorkspaceViewModel.cs`
- Test: `tests/Cmux.Tests/ProjectConfigTests.cs`

- [ ] **Step 1: Write ProjectConfig tests**

Create `tests/Cmux.Tests/ProjectConfigTests.cs`:

```csharp
using Cmux.Core.Models;
using Cmux.Core.Services;
using FluentAssertions;

namespace Cmux.Tests;

public class ProjectConfigTests
{
    [Fact]
    public void Parse_ValidJson_ReturnsConfig()
    {
        var json = """
        {
            "name": "My Project",
            "color": "#FF5733",
            "env": { "NODE_ENV": "development", "PORT": "3000" }
        }
        """;
        var config = ProjectConfigService.Parse(json);
        config.Should().NotBeNull();
        config!.Name.Should().Be("My Project");
        config.Color.Should().Be("#FF5733");
        config.Env.Should().ContainKey("NODE_ENV").WhoseValue.Should().Be("development");
    }

    [Fact]
    public void Parse_EmptyJson_ReturnsEmptyConfig()
    {
        var config = ProjectConfigService.Parse("{}");
        config.Should().NotBeNull();
        config!.Name.Should().BeNull();
        config.Env.Should().BeEmpty();
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var config = ProjectConfigService.Parse("not json");
        config.Should().BeNull();
    }

    [Fact]
    public void FindConfigPath_ReturnsNullForNonExistentDir()
    {
        var path = ProjectConfigService.FindConfigPath(@"C:\nonexistent\path\that\does\not\exist");
        path.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cmux.Tests/ --filter "FullyQualifiedName~ProjectConfigTests" -v n`
Expected: Build error.

- [ ] **Step 3: Create ProjectConfig model**

Create `src/Cmux.Core/Models/ProjectConfig.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Cmux.Core.Models;

public class ProjectConfig
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; set; } = new();

    [JsonPropertyName("shell")]
    public string? Shell { get; set; }

    [JsonPropertyName("startDirectory")]
    public string? StartDirectory { get; set; }
}
```

- [ ] **Step 4: Implement ProjectConfigService**

Create `src/Cmux.Core/Services/ProjectConfigService.cs`:

```csharp
using System.Text.Json;
using Cmux.Core.Models;

namespace Cmux.Core.Services;

public static class ProjectConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ProjectConfig? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProjectConfig>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public static string? FindConfigPath(string? directory)
    {
        if (string.IsNullOrEmpty(directory)) return null;

        var current = directory;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, ".cmux", "cmux.json");
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(current, "cmux.json");
            if (File.Exists(candidate)) return candidate;

            if (string.Equals(current, home, StringComparison.OrdinalIgnoreCase))
                break;

            var parent = Directory.GetParent(current)?.FullName;
            if (parent == current) break;
            current = parent;
        }

        return null;
    }

    public static ProjectConfig? LoadForDirectory(string? directory)
    {
        var path = FindConfigPath(directory);
        if (path == null) return null;
        try
        {
            var json = File.ReadAllText(path);
            return Parse(json);
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Cmux.Tests/ --filter "FullyQualifiedName~ProjectConfigTests" -v n`
Expected: All 4 tests pass.

- [ ] **Step 6: Apply project config when workspace CWD changes**

In `src/Cmux/ViewModels/WorkspaceViewModel.cs`, in the method that handles CWD changes (e.g., `OnSurfaceWorkingDirectoryChanged` or `RefreshInfo`), add project config loading:

```csharp
private string? _lastConfigPath;

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
```

Call `TryApplyProjectConfig(dir)` from `RefreshInfo()` after getting the working directory.

- [ ] **Step 7: Build and verify**

Run: `dotnet build src/Cmux/Cmux.csproj`
Expected: Build succeeds.

- [ ] **Step 8: Commit**

```bash
git add src/Cmux.Core/Models/ProjectConfig.cs src/Cmux.Core/Services/ProjectConfigService.cs tests/Cmux.Tests/ProjectConfigTests.cs src/Cmux/ViewModels/WorkspaceViewModel.cs
git commit -m "feat: add .cmux/cmux.json project-level configuration"
```

---

## Task 9: Workspace Groups

**Files:**
- Create: `src/Cmux.Core/Models/WorkspaceGroup.cs`
- Create: `src/Cmux/Controls/WorkspaceGroupHeader.xaml`
- Create: `src/Cmux/Controls/WorkspaceGroupHeader.xaml.cs`
- Modify: `src/Cmux/ViewModels/MainViewModel.cs`
- Modify: `src/Cmux/ViewModels/WorkspaceViewModel.cs`
- Modify: `src/Cmux/Views/MainWindow.xaml`
- Modify: `src/Cmux.Core/Models/SessionState.cs`
- Modify: `src/Cmux/Controls/WorkspaceSidebarItem.xaml`

- [ ] **Step 1: Create WorkspaceGroup model**

Create `src/Cmux.Core/Models/WorkspaceGroup.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Cmux.Core.Models;

public class WorkspaceGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Group";

    [JsonPropertyName("isCollapsed")]
    public bool IsCollapsed { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}
```

- [ ] **Step 2: Add GroupId to Workspace model**

In `src/Cmux.Core/Models/Workspace.cs`, add:

```csharp
public string? GroupId { get; set; }
```

- [ ] **Step 3: Add group support to MainViewModel**

In `src/Cmux/ViewModels/MainViewModel.cs`, add:

```csharp
[ObservableProperty]
private ObservableCollection<WorkspaceGroup> _workspaceGroups = new();

[RelayCommand]
private void CreateGroup(string name)
{
    var group = new WorkspaceGroup { Name = name };
    WorkspaceGroups.Add(group);
    EventBus.Publish("group.created", new { id = group.Id, name });
}

[RelayCommand]
private void DeleteGroup(string groupId)
{
    var group = WorkspaceGroups.FirstOrDefault(g => g.Id == groupId);
    if (group == null) return;

    foreach (var ws in Workspaces.Where(w => w.Workspace.GroupId == groupId))
        ws.Workspace.GroupId = null;

    WorkspaceGroups.Remove(group);
}

[RelayCommand]
private void ToggleGroupCollapsed(string groupId)
{
    var group = WorkspaceGroups.FirstOrDefault(g => g.Id == groupId);
    if (group != null)
        group.IsCollapsed = !group.IsCollapsed;
    OnPropertyChanged(nameof(SidebarItems));
}

public IEnumerable<object> SidebarItems
{
    get
    {
        var ungrouped = Workspaces.Where(w => w.Workspace.GroupId == null);
        foreach (var ws in ungrouped)
            yield return ws;

        foreach (var group in WorkspaceGroups)
        {
            yield return group;
            if (!group.IsCollapsed)
            {
                foreach (var ws in Workspaces.Where(w => w.Workspace.GroupId == group.Id))
                    yield return ws;
            }
        }
    }
}

public void MoveWorkspaceToGroup(WorkspaceViewModel ws, string? groupId)
{
    ws.Workspace.GroupId = groupId;
    OnPropertyChanged(nameof(SidebarItems));
}
```

- [ ] **Step 4: Add group persistence to SessionState**

In `src/Cmux.Core/Models/SessionState.cs`, add to `SessionState`:

```csharp
[JsonPropertyName("workspaceGroups")]
public List<WorkspaceGroup>? WorkspaceGroups { get; set; }
```

In `WorkspaceState`, add:

```csharp
[JsonPropertyName("groupId")]
public string? GroupId { get; set; }
```

Update save logic to persist groups:

```csharp
WorkspaceGroups = viewModel.WorkspaceGroups.ToList(),
```

And `GroupId = ws.Workspace.GroupId` in each `WorkspaceState`.

Update restore logic to rebuild groups:

```csharp
if (session.WorkspaceGroups != null)
    foreach (var g in session.WorkspaceGroups)
        WorkspaceGroups.Add(g);
```

And set `workspace.GroupId = wsState.GroupId` during restore.

- [ ] **Step 5: Create WorkspaceGroupHeader control**

Create `src/Cmux/Controls/WorkspaceGroupHeader.xaml`:

```xml
<UserControl x:Class="Cmux.Controls.WorkspaceGroupHeader"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:models="clr-namespace:Cmux.Core.Models;assembly=Cmux.Core">
    <Border Background="Transparent" Padding="8,4" Cursor="Hand"
            MouseLeftButtonUp="OnHeaderClick">
        <StackPanel Orientation="Horizontal">
            <TextBlock x:Name="Arrow" Text="&#xE76C;" FontFamily="Segoe MDL2 Assets"
                       FontSize="10" Foreground="{DynamicResource ForegroundDimBrush}"
                       VerticalAlignment="Center" Margin="0,0,6,0"
                       RenderTransformOrigin="0.5,0.5">
                <TextBlock.RenderTransform>
                    <RotateTransform x:Name="ArrowRotation" Angle="0"/>
                </TextBlock.RenderTransform>
            </TextBlock>
            <TextBlock Text="{Binding Name}" FontSize="11" FontWeight="SemiBold"
                       Foreground="{DynamicResource ForegroundDimBrush}"
                       VerticalAlignment="Center"/>
        </StackPanel>
    </Border>
</UserControl>
```

Create `src/Cmux/Controls/WorkspaceGroupHeader.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using Cmux.Core.Models;

namespace Cmux.Controls;

public partial class WorkspaceGroupHeader : UserControl
{
    public event Action<WorkspaceGroup>? ToggleCollapsed;

    public WorkspaceGroupHeader()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateArrow();
    }

    private void OnHeaderClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is WorkspaceGroup group)
        {
            ToggleCollapsed?.Invoke(group);
            UpdateArrow();
        }
    }

    private void UpdateArrow()
    {
        if (DataContext is WorkspaceGroup group)
            ArrowRotation.Angle = group.IsCollapsed ? -90 : 0;
    }
}
```

- [ ] **Step 6: Update sidebar to use data template selector**

In `src/Cmux/Views/MainWindow.xaml`, the sidebar ListBox currently uses `ItemsSource="{Binding Workspaces}"`. Change it to bind to `SidebarItems` and use a `DataTemplateSelector` to distinguish between `WorkspaceViewModel` (existing template) and `WorkspaceGroup` (new group header template).

Create a `SidebarTemplateSelector` in `MainWindow.xaml.cs`:

```csharp
public class SidebarTemplateSelector : DataTemplateSelector
{
    public DataTemplate? WorkspaceTemplate { get; set; }
    public DataTemplate? GroupTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item switch
        {
            WorkspaceViewModel => WorkspaceTemplate,
            WorkspaceGroup => GroupTemplate,
            _ => base.SelectTemplate(item, container),
        };
}
```

- [ ] **Step 7: Add "Move to Group" context menu to WorkspaceSidebarItem**

In `src/Cmux/Controls/WorkspaceSidebarItem.xaml`, add to the context menu:

```xml
<MenuItem Header="{DynamicResource Workspace_MoveToGroup}">
    <!-- Sub-items populated in code-behind -->
</MenuItem>
```

In code-behind, populate dynamically with available groups + "New Group" + "Remove from Group".

- [ ] **Step 8: Add i18n strings**

In `Strings.en.xaml`:
```xml
<system:String x:Key="Workspace_MoveToGroup">Move to Group</system:String>
<system:String x:Key="Workspace_NewGroup">New Group...</system:String>
<system:String x:Key="Workspace_RemoveFromGroup">Remove from Group</system:String>
```

In `Strings.zh.xaml`:
```xml
<system:String x:Key="Workspace_MoveToGroup">移动到分组</system:String>
<system:String x:Key="Workspace_NewGroup">新建分组...</system:String>
<system:String x:Key="Workspace_RemoveFromGroup">移出分组</system:String>
```

- [ ] **Step 9: Build and verify**

Run: `dotnet build src/Cmux/Cmux.csproj`
Expected: Build succeeds.

- [ ] **Step 10: Commit**

```bash
git add src/Cmux.Core/Models/WorkspaceGroup.cs src/Cmux/Controls/WorkspaceGroupHeader.xaml src/Cmux/Controls/WorkspaceGroupHeader.xaml.cs src/Cmux/ViewModels/MainViewModel.cs src/Cmux/ViewModels/WorkspaceViewModel.cs src/Cmux/Views/MainWindow.xaml src/Cmux/Views/MainWindow.xaml.cs src/Cmux.Core/Models/SessionState.cs src/Cmux/Controls/WorkspaceSidebarItem.xaml src/Cmux/Strings/Strings.en.xaml src/Cmux/Strings/Strings.zh.xaml
git commit -m "feat: add collapsible workspace groups in sidebar"
```

---

## Task 10: Agent Hook System

**Files:**
- Create: `src/Cmux.Core/Models/AgentHookEvent.cs`
- Create: `src/Cmux.Core/Services/AgentHookService.cs`
- Modify: `src/Cmux/ViewModels/MainViewModel.cs`
- Modify: `src/Cmux.Core/Services/AgentDetector.cs`
- Modify: `src/Cmux.Cli/Program.cs`
- Test: `tests/Cmux.Tests/AgentHookTests.cs`

- [ ] **Step 1: Write AgentHook tests**

Create `tests/Cmux.Tests/AgentHookTests.cs`:

```csharp
using Cmux.Core.Models;
using Cmux.Core.Services;
using FluentAssertions;

namespace Cmux.Tests;

public class AgentHookTests
{
    [Fact]
    public void ParseHookEvent_ValidJson_ReturnsEvent()
    {
        var json = """
        {
            "agent": "claude-code",
            "event": "stop",
            "sessionId": "abc123",
            "workspaceId": "ws1",
            "surfaceId": "sf1"
        }
        """;
        var evt = AgentHookService.ParseEvent(json);
        evt.Should().NotBeNull();
        evt!.Agent.Should().Be("claude-code");
        evt.Event.Should().Be("stop");
        evt.SessionId.Should().Be("abc123");
    }

    [Fact]
    public void ParseHookEvent_InvalidJson_ReturnsNull()
    {
        var evt = AgentHookService.ParseEvent("not json");
        evt.Should().BeNull();
    }

    [Fact]
    public void ParseHookEvent_MinimalPayload_ReturnsEvent()
    {
        var json = """{"agent": "codex", "event": "notification"}""";
        var evt = AgentHookService.ParseEvent(json);
        evt.Should().NotBeNull();
        evt!.Agent.Should().Be("codex");
        evt.Event.Should().Be("notification");
    }

    [Fact]
    public void ClassifyEvent_StopEvent_IsNotification()
    {
        var evt = new AgentHookEvent { Agent = "claude-code", Event = "stop" };
        var action = AgentHookService.ClassifyEvent(evt);
        action.Should().Be(HookAction.Notify);
    }

    [Fact]
    public void ClassifyEvent_PermissionRequest_IsApproval()
    {
        var evt = new AgentHookEvent { Agent = "claude-code", Event = "permission-request" };
        var action = AgentHookService.ClassifyEvent(evt);
        action.Should().Be(HookAction.Approval);
    }

    [Fact]
    public void ClassifyEvent_SessionStart_IsSessionStart()
    {
        var evt = new AgentHookEvent { Agent = "claude-code", Event = "session-start" };
        var action = AgentHookService.ClassifyEvent(evt);
        action.Should().Be(HookAction.SessionStart);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Cmux.Tests/ --filter "FullyQualifiedName~AgentHookTests" -v n`
Expected: Build error.

- [ ] **Step 3: Create AgentHookEvent model**

Create `src/Cmux.Core/Models/AgentHookEvent.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Cmux.Core.Models;

public class AgentHookEvent
{
    [JsonPropertyName("agent")]
    public string Agent { get; set; } = "";

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("surfaceId")]
    public string? SurfaceId { get; set; }

    [JsonPropertyName("paneId")]
    public string? PaneId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("tool")]
    public string? Tool { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }
}

public enum HookAction
{
    Notify,
    Approval,
    SessionStart,
    Telemetry,
}
```

- [ ] **Step 4: Implement AgentHookService**

Create `src/Cmux.Core/Services/AgentHookService.cs`:

```csharp
using System.Text.Json;
using Cmux.Core.Models;

namespace Cmux.Core.Services;

public static class AgentHookService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static AgentHookEvent? ParseEvent(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AgentHookEvent>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static HookAction ClassifyEvent(AgentHookEvent evt)
    {
        return evt.Event.ToLowerInvariant() switch
        {
            "stop" => HookAction.Notify,
            "notification" => HookAction.Notify,
            "session-start" or "session_start" or "sessionstart" => HookAction.SessionStart,
            "permission-request" or "permission_request" or "permissionrequest" => HookAction.Approval,
            "pre-tool-use" or "pre_tool_use" or "pretooluse" => ClassifyToolUse(evt),
            _ => HookAction.Telemetry,
        };
    }

    private static HookAction ClassifyToolUse(AgentHookEvent evt)
    {
        var tool = evt.Tool?.ToLowerInvariant() ?? "";
        var readOnlyTools = new[] { "read", "grep", "glob", "search", "list", "view", "cat" };
        if (readOnlyTools.Any(t => tool.Contains(t)))
            return HookAction.Telemetry;
        return HookAction.Approval;
    }

    public static string GetHookCommand(string pipeName)
    {
        return $"cmux hooks --pipe {pipeName}";
    }

    public static Dictionary<string, string> GetClaudeHookConfig(string pipeName)
    {
        var cmd = GetHookCommand(pipeName);
        return new Dictionary<string, string>
        {
            ["Stop"] = cmd,
            ["Notification"] = cmd,
            ["PreToolUse"] = cmd,
        };
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Cmux.Tests/ --filter "FullyQualifiedName~AgentHookTests" -v n`
Expected: All 6 tests pass.

- [ ] **Step 6: Add `hooks` IPC command**

In `MainViewModel.HandlePipeCommand`, add:

```csharp
"HOOKS" => HandleHookEvent(args),
```

```csharp
private string HandleHookEvent(Dictionary<string, string> args)
{
    var json = args.GetValueOrDefault("payload", "{}");
    var evt = AgentHookService.ParseEvent(json);
    if (evt == null)
        return JsonSerializer.Serialize(new { error = "invalid hook payload" });

    var action = AgentHookService.ClassifyEvent(evt);

    switch (action)
    {
        case HookAction.Notify:
            var workspaceId = evt.WorkspaceId ?? SelectedWorkspace?.Workspace.Id ?? "";
            var surfaceId = evt.SurfaceId ?? SelectedWorkspace?.SelectedSurface?.Surface.Id ?? "";
            App.NotificationService.AddNotification(
                workspaceId, surfaceId, evt.PaneId,
                evt.Title ?? $"{evt.Agent} — {evt.Event}",
                null,
                evt.Body ?? "",
                NotificationSource.Cli);
            break;

        case HookAction.SessionStart:
            // Record session ID for resume support
            if (evt.SessionId != null && evt.WorkspaceId != null)
            {
                var ws = Workspaces.FirstOrDefault(w => w.Workspace.Id == evt.WorkspaceId);
                if (ws != null)
                    ws.AgentSessionId = evt.SessionId;
            }
            break;

        case HookAction.Approval:
            // Send notification with approval action
            var wsId = evt.WorkspaceId ?? SelectedWorkspace?.Workspace.Id ?? "";
            var sfId = evt.SurfaceId ?? SelectedWorkspace?.SelectedSurface?.Surface.Id ?? "";
            App.NotificationService.AddNotification(
                wsId, sfId, evt.PaneId,
                $"🔐 {evt.Agent} needs approval",
                evt.Tool,
                evt.Command ?? evt.Body ?? "Permission requested",
                NotificationSource.Cli);
            break;
    }

    EventBus.Publish("agent.hook", new { agent = evt.Agent, @event = evt.Event, action = action.ToString() });

    return JsonSerializer.Serialize(new { ok = true, action = action.ToString() });
}
```

- [ ] **Step 7: Add CLI `hooks` subcommand**

In `src/Cmux.Cli/Program.cs`, add:

```csharp
case "hooks":
    return await HandleHooks(subArgs);
```

```csharp
static async Task<int> HandleHooks(string[] args)
{
    string? payload = null;

    // Read from stdin if piped
    if (Console.IsInputRedirected)
        payload = await Console.In.ReadToEndAsync();

    var cmdArgs = ParseKeyValues(args);
    if (payload != null)
        cmdArgs["payload"] = payload;
    else if (args.Length >= 2)
        cmdArgs["payload"] = args.Length > 2
            ? System.Text.Json.JsonSerializer.Serialize(new { agent = args[0], @event = args[1] })
            : args[1];

    return await SendAndPrint("HOOKS", cmdArgs);
}
```

- [ ] **Step 8: Build and verify**

Run: `dotnet build`
Expected: All projects build.

- [ ] **Step 9: Commit**

```bash
git add src/Cmux.Core/Models/AgentHookEvent.cs src/Cmux.Core/Services/AgentHookService.cs tests/Cmux.Tests/AgentHookTests.cs src/Cmux/ViewModels/MainViewModel.cs src/Cmux.Cli/Program.cs
git commit -m "feat: add agent hook system for AI agent lifecycle events"
```

---

## Task 11: Agent Session Resume

**Files:**
- Modify: `src/Cmux/ViewModels/WorkspaceViewModel.cs`
- Modify: `src/Cmux.Core/Models/SessionState.cs`
- Modify: `src/Cmux.Core/Services/AgentDetector.cs`
- Modify: `src/Cmux/ViewModels/MainViewModel.cs`
- Modify: `src/Cmux/ViewModels/SurfaceViewModel.cs`

- [ ] **Step 1: Add AgentSessionId to WorkspaceViewModel**

In `src/Cmux/ViewModels/WorkspaceViewModel.cs`, add:

```csharp
[ObservableProperty]
private string? _agentSessionId;

[ObservableProperty]
private string? _agentSessionAgent;
```

In the existing `RefreshInfo()` method, after agent detection, check if the detected agent exposes a session ID. Add to the agent detection block:

```csharp
if (DetectedAgent != AgentType.None && focusedSession?.ShellProcessId is int agentPid)
{
    var sessionId = AgentDetector.GetSessionId(DetectedAgent, agentPid);
    if (sessionId != null)
    {
        AgentSessionId = sessionId;
        AgentSessionAgent = DetectedAgent.ToString();
    }
}
```

- [ ] **Step 2: Add GetSessionId to AgentDetector**

In `src/Cmux.Core/Services/AgentDetector.cs`, add:

```csharp
public static string? GetSessionId(AgentType agent, int pid)
{
    try
    {
        return agent switch
        {
            AgentType.ClaudeCode => GetClaudeSessionId(pid),
            AgentType.Codex => GetCodexSessionId(pid),
            _ => null,
        };
    }
    catch
    {
        return null;
    }
}

private static string? GetClaudeSessionId(int pid)
{
    // Claude Code stores session in command line args or env
    var cmdLine = GetCommandLine(pid);
    if (cmdLine == null) return null;

    // Look for --resume <id> or session ID in args
    var resumeIdx = cmdLine.IndexOf("--resume", StringComparison.OrdinalIgnoreCase);
    if (resumeIdx >= 0)
    {
        var after = cmdLine[(resumeIdx + 9)..].Trim();
        var spaceIdx = after.IndexOf(' ');
        return spaceIdx > 0 ? after[..spaceIdx] : after;
    }

    // Look for session ID in CLAUDE_SESSION_ID env var
    // This requires reading process environment which is complex on Windows.
    // Fallback: scan ~/.claude/projects/ for recent session files
    return null;
}

private static string? GetCodexSessionId(int pid)
{
    var cmdLine = GetCommandLine(pid);
    if (cmdLine == null) return null;
    var resumeIdx = cmdLine.IndexOf("resume", StringComparison.OrdinalIgnoreCase);
    if (resumeIdx >= 0)
    {
        var after = cmdLine[(resumeIdx + 7)..].Trim();
        var spaceIdx = after.IndexOf(' ');
        return spaceIdx > 0 ? after[..spaceIdx] : after;
    }
    return null;
}

private static string? GetCommandLine(int pid)
{
    try
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
        foreach (var obj in searcher.Get())
            return obj["CommandLine"]?.ToString();
    }
    catch { }
    return null;
}
```

- [ ] **Step 3: Persist agent session in SessionState**

In `src/Cmux.Core/Models/SessionState.cs`, add to `WorkspaceState`:

```csharp
[JsonPropertyName("agentSessionId")]
public string? AgentSessionId { get; set; }

[JsonPropertyName("agentSessionAgent")]
public string? AgentSessionAgent { get; set; }
```

Update save/restore to include these fields.

- [ ] **Step 4: Add resume logic on session restore**

In `src/Cmux/ViewModels/SurfaceViewModel.cs` (or `WorkspaceViewModel`), after restoring the session, if an agent session ID is present, queue a resume command:

```csharp
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

    // Write resume command to the focused pane after a short delay
    Task.Run(async () =>
    {
        await Task.Delay(2000); // Wait for shell to be ready
        var focusedSession = SelectedSurface?.GetFocusedSession();
        focusedSession?.Write(resumeCmd + "\r");
    });
}
```

Call this from the restore path in `MainViewModel` after all workspaces are rebuilt:

```csharp
foreach (var ws in Workspaces)
{
    if (ws.AgentSessionId != null)
        ws.TryResumeAgentSession();
}
```

- [ ] **Step 5: Add command palette command for resume**

In `MainWindow.BuildPaletteItems()`, add:

```csharp
new PaletteItem
{
    Id = "agent_resume",
    Label = LanguageService.Lang("Agent_Resume"),
    Description = LanguageService.Lang("Agent_Resume_Desc"),
    Icon = "",
    Category = "Agent",
    Execute = () => ViewModel?.SelectedWorkspace?.TryResumeAgentSession(),
},
```

- [ ] **Step 6: Add i18n strings**

In `Strings.en.xaml`:
```xml
<system:String x:Key="Agent_Resume">Resume Agent Session</system:String>
<system:String x:Key="Agent_Resume_Desc">Resume the last AI agent session in this workspace</system:String>
```

In `Strings.zh.xaml`:
```xml
<system:String x:Key="Agent_Resume">恢复 Agent 会话</system:String>
<system:String x:Key="Agent_Resume_Desc">恢复此工作区最后的 AI Agent 会话</system:String>
```

- [ ] **Step 7: Build and verify**

Run: `dotnet build src/Cmux/Cmux.csproj`
Expected: Build succeeds.

- [ ] **Step 8: Commit**

```bash
git add src/Cmux/ViewModels/WorkspaceViewModel.cs src/Cmux.Core/Services/AgentDetector.cs src/Cmux.Core/Models/SessionState.cs src/Cmux/ViewModels/MainViewModel.cs src/Cmux/ViewModels/SurfaceViewModel.cs src/Cmux/Views/MainWindow.xaml.cs src/Cmux/Strings/Strings.en.xaml src/Cmux/Strings/Strings.zh.xaml
git commit -m "feat: add agent session resume on app restart"
```

---

## Summary

| Task | Feature | Estimated Effort |
|------|---------|-----------------|
| 1 | Git Dirty State | Small |
| 2 | Port Display | Small |
| 3 | Workspace Env Vars | Medium |
| 4 | Fuzzy Command Palette | Medium |
| 5 | Chord Shortcuts | Small |
| 6 | Sidebar Status Entries | Medium |
| 7 | Event Bus + Stream | Medium |
| 8 | Project Config File | Medium |
| 9 | Workspace Groups | Large |
| 10 | Agent Hook System | Large |
| 11 | Agent Session Resume | Medium |

All tasks are independent — they can be implemented in any order or in parallel. Tasks 1-2 and 5 are the quickest wins. Tasks 9-10 are the most complex.
