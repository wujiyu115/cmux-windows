using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Cmux.Core.Config;
using Cmux.Core.Models;
using Cmux.Core.Services;
using Cmux.Services;

namespace Cmux.Views;

public partial class WorkspaceCreationWindow : Window
{
    public string WorkspaceName => NameBox.Text;
    public string? SelectedShell { get; private set; }
    public string SelectedColor { get; private set; } = "#FF818CF8";
    public string? SelectedGroupId { get; private set; }
    public string? SelectedWorkingDirectory { get; private set; }

    public WorkspaceCreationWindow(string defaultName, IEnumerable<WorkspaceGroup>? groups = null)
    {
        InitializeComponent();
        WindowAppearance.Apply(this);

        NameBox.Text = defaultName;

        var shells = ShellDetector.DetectShells();
        var configuredDefault = SettingsService.Current.DefaultShell;
        var items = new List<ShellDisplayItem>
        {
            new(LanguageService.Lang("Workspace_ShellGlobalDefault"), null, false),
        };
        foreach (var s in shells)
            items.Add(new ShellDisplayItem(s.Name, s.Path,
                string.Equals(s.Path, configuredDefault, StringComparison.OrdinalIgnoreCase)));

        ShellCombo.ItemsSource = items;
        ShellCombo.SelectedValuePath = "Path";
        ShellCombo.SelectedIndex = 0;
        ShellCombo.SelectionChanged += ShellCombo_SelectionChanged;

        var groupItems = new List<GroupDisplayItem>
        {
            new(LanguageService.Lang("WorkspaceCreate_NoGroup"), null),
        };
        if (groups != null)
            foreach (var g in groups)
                groupItems.Add(new GroupDisplayItem(g.Name, g.Id));

        GroupCombo.ItemsSource = groupItems;
        GroupCombo.DisplayMemberPath = "Name";
        GroupCombo.SelectedValuePath = "Id";
        GroupCombo.SelectedIndex = 0;

        HighlightColorButton("#FF818CF8");

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        SelectedShell = ShellCombo.SelectedValue as string;
        SelectedGroupId = GroupCombo.SelectedValue as string;
        SelectedWorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirBox.Text) ? null : WorkingDirBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = LanguageService.Lang("WorkspaceCreate_WorkingDirectory"),
        };

        // Pre-select the current value if it points at a real directory.
        var current = WorkingDirBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(current) && System.IO.Directory.Exists(current))
            dialog.InitialDirectory = current;

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            WorkingDirBox.Text = dialog.FolderName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Create_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
    }

    private void ColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string color)
        {
            SelectedColor = color;
            HighlightColorButton(color);
        }
    }

    private void ColorCustom_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPickerWindow(SelectedColor)
        {
            Owner = this,
        };

        if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.SelectedHex))
        {
            SelectedColor = picker.SelectedHex;
            HighlightColorButton(null);
        }
    }

    private void HighlightColorButton(string? activeColor)
    {
        foreach (var child in ColorPanel.Children)
        {
            if (child is Button btn)
            {
                btn.BorderThickness = new Thickness(
                    btn.Tag is string tag && string.Equals(tag, activeColor, StringComparison.OrdinalIgnoreCase) ? 2 : 0);
                btn.BorderBrush = btn.BorderThickness.Left > 0
                    ? (Brush)FindResource("AccentBrush")
                    : Brushes.Transparent;
            }
        }
    }

    private void ShellCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShellCombo.ItemsSource is not List<ShellDisplayItem> items)
            return;
        var selected = ShellCombo.SelectedItem as ShellDisplayItem;
        foreach (var item in items)
            item.IsCurrentDefault = item == selected;
    }

    private class ShellDisplayItem : INotifyPropertyChanged
    {
        public string Name { get; }
        public string? Path { get; }

        private bool _isCurrentDefault;
        public bool IsCurrentDefault
        {
            get => _isCurrentDefault;
            set
            {
                if (_isCurrentDefault == value) return;
                _isCurrentDefault = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrentDefault)));
            }
        }

        public ShellDisplayItem(string name, string? path, bool isCurrentDefault)
        {
            Name = name;
            Path = path;
            _isCurrentDefault = isCurrentDefault;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private record GroupDisplayItem(string Name, string? Id);
}
