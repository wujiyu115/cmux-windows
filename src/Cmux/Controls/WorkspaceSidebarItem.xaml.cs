using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cmux.Core.Models;
using Cmux.Core.Services;
using Cmux.Services;
using Cmux.ViewModels;
using Cmux.Views;

namespace Cmux.Controls;

public partial class WorkspaceSidebarItem : UserControl
{
    public WorkspaceSidebarItem()
    {
        InitializeComponent();
    }

    private WorkspaceViewModel? Vm => DataContext as WorkspaceViewModel;
    private MainViewModel? MainVm => FindMainViewModel();

    private void Rename_Click(object sender, RoutedEventArgs e) => StartRename();

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (MainVm is { } main && Vm is { } ws)
        {
            main.DuplicateWorkspace(ws);
        }
    }

    private void NewSurface_Click(object sender, RoutedEventArgs e) => Vm?.CreateNewSurface();

    private void SetIcon_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;

        var prompt = new TextPromptWindow(
            title: LanguageService.Lang("Workspace_IconTitle"),
            message: LanguageService.Lang("Workspace_IconMessage"),
            defaultValue: Vm.IconGlyph)
        {
            Owner = Window.GetWindow(this),
        };

        if (prompt.ShowDialog() != true)
            return;

        var input = prompt.ResponseText;
        if (string.IsNullOrWhiteSpace(input))
            return;

        var value = input.Trim();

        if (value.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
        {
            ThemedMessageBox.Show(LanguageService.Lang("Workspace_SvgNotSupported"),
                LanguageService.Lang("Workspace_IconTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (TryParseHexGlyph(value, out var glyph))
            Vm.IconGlyph = glyph;
        else
            Vm.IconGlyph = value;
    }

    private void SetColor_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null || sender is not MenuItem item || item.Tag is not string color)
            return;

        Vm.AccentColor = color;
    }

    private void SetCustomColor_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null)
            return;

        var picker = new ColorPickerWindow(Vm.AccentColor)
        {
            Owner = Window.GetWindow(this),
        };

        if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedHex))
            Vm.AccentColor = picker.SelectedHex;
    }

    private void SetWorkingDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;

        var prompt = new TextPromptWindow(
            title: LanguageService.Lang("Workspace_SetWorkingDirectory"),
            message: LanguageService.Lang("Workspace_WorkingDirectoryMessage"),
            defaultValue: Vm.StartDirectory ?? "")
        {
            Owner = Window.GetWindow(this),
        };

        if (prompt.ShowDialog() == true)
            Vm.StartDirectory = string.IsNullOrWhiteSpace(prompt.ResponseText) ? null : prompt.ResponseText.Trim();
    }

    private void EnvVars_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;

        var current = string.Join("\n", Vm.Workspace.EnvironmentVariables.Select(kv => $"{kv.Key}={kv.Value}"));

        var prompt = new TextPromptWindow(
            title: LanguageService.Lang("Workspace_EnvVars"),
            message: LanguageService.Lang("Workspace_EnvVarsMessage"),
            defaultValue: current,
            multiLine: true)
        {
            Owner = Window.GetWindow(this),
        };

        if (prompt.ShowDialog() == true)
        {
            Vm.Workspace.EnvironmentVariables.Clear();
            foreach (var line in prompt.ResponseText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = line.IndexOf('=');
                if (eq > 0)
                    Vm.Workspace.EnvironmentVariables[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (MainVm is { } main && Vm is { } ws)
        {
            int idx = main.Workspaces.IndexOf(ws);
            if (idx > 0) main.Workspaces.Move(idx, idx - 1);
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (MainVm is { } main && Vm is { } ws)
        {
            int idx = main.Workspaces.IndexOf(ws);
            if (idx >= 0 && idx < main.Workspaces.Count - 1)
                main.Workspaces.Move(idx, idx + 1);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (MainVm is { } main && Vm is { } ws)
            main.CloseWorkspace(ws);
    }

    private void StartRename()
    {
        NameDisplay.Visibility = Visibility.Collapsed;
        NameEditor.Visibility = Visibility.Visible;
        NameEditor.SelectAll();
        NameEditor.Focus();
    }

    private void FinishRename()
    {
        NameEditor.Visibility = Visibility.Collapsed;
        NameDisplay.Visibility = Visibility.Visible;
    }

    private void NameDisplay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            StartRename();
            e.Handled = true;
        }
    }

    private void NameEditor_LostFocus(object sender, RoutedEventArgs e) => FinishRename();

    private void NameEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Escape)
        {
            if (e.Key == Key.Escape && Vm != null)
                NameEditor.Text = Vm.Name; // revert
            FinishRename();
            e.Handled = true;
        }
    }

    private static bool TryParseHexGlyph(string input, out string glyph)
    {
        glyph = string.Empty;

        var normalized = input.Trim();
        if (normalized.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];
        else if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];

        if (!uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
            return false;

        if (codePoint > 0x10FFFF)
            return false;

        glyph = char.ConvertFromUtf32((int)codePoint);
        return true;
    }

    private void DefaultTerminalMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menu || Vm is null) return;

        menu.Items.Clear();

        var globalItem = new MenuItem
        {
            Header = LanguageService.Lang("Workspace_ShellGlobalDefault"),
        };
        if (string.IsNullOrWhiteSpace(Vm.DefaultShell))
            globalItem.Icon = "";
        globalItem.Click += (_, _) => Vm.DefaultShell = null;
        menu.Items.Add(globalItem);

        menu.Items.Add(new Separator());

        var shells = ShellDetector.DetectShells();
        foreach (var shell in shells)
        {
            var item = new MenuItem
            {
                Header = shell.Name,
                Tag = shell.Path,
            };
            item.Icon = string.Equals(Vm.DefaultShell, shell.Path, StringComparison.OrdinalIgnoreCase)
                ? ""
                : null;

            item.Click += (s, _) =>
            {
                if (s is MenuItem mi && mi.Tag is string path)
                    Vm.DefaultShell = path;
            };
            menu.Items.Add(item);
        }
    }

    private void MoveToGroupMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        var menu = sender as MenuItem;
        var vm = FindMainViewModel();
        if (menu == null || vm == null) return;

        // Remove dynamic group items (keep the static ones: Remove, Separator, New)
        while (menu.Items.Count > 3)
            menu.Items.RemoveAt(3);

        foreach (var group in vm.WorkspaceGroups)
        {
            var item = new MenuItem { Header = group.Name, Tag = group.Id };
            item.Click += (s, _) =>
            {
                if (DataContext is WorkspaceViewModel ws && s is MenuItem mi)
                    vm.MoveWorkspaceToGroup(ws, mi.Tag as string);
            };
            menu.Items.Add(item);
        }
    }

    private void RemoveFromGroup_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel ws)
            FindMainViewModel()?.MoveWorkspaceToGroup(ws, null);
    }

    private void NewGroup_Click(object sender, RoutedEventArgs e)
    {
        var vm = FindMainViewModel();
        if (vm == null || DataContext is not WorkspaceViewModel ws) return;

        var dialog = new TextPromptWindow(
            LanguageService.Lang("Workspace_NewGroup"),
            LanguageService.Lang("Workspace_NewGroup"))
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
        {
            vm.CreateGroup(dialog.ResponseText.Trim());
            var newGroup = vm.WorkspaceGroups.LastOrDefault();
            if (newGroup != null)
                vm.MoveWorkspaceToGroup(ws, newGroup.Id);
        }
    }

    private MainViewModel? FindMainViewModel()
    {
        var window = Window.GetWindow(this);
        return window?.DataContext as MainViewModel;
    }
}
