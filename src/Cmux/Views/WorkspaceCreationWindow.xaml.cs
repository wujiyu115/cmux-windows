using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Cmux.Core.Services;
using Cmux.Services;

namespace Cmux.Views;

public partial class WorkspaceCreationWindow : Window
{
    public string WorkspaceName => NameBox.Text;
    public string? SelectedShell { get; private set; }
    public string SelectedColor { get; private set; } = "#FF818CF8";

    public WorkspaceCreationWindow(string defaultName)
    {
        InitializeComponent();
        WindowAppearance.Apply(this);

        NameBox.Text = defaultName;

        var shells = ShellDetector.DetectShells();
        var items = new List<ShellDisplayItem>
        {
            new(LanguageService.Lang("Workspace_ShellGlobalDefault"), null),
        };
        foreach (var s in shells)
            items.Add(new ShellDisplayItem(s.Name, s.Path));

        ShellCombo.ItemsSource = items;
        ShellCombo.DisplayMemberPath = "Name";
        ShellCombo.SelectedValuePath = "Path";
        ShellCombo.SelectedIndex = 0;

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
        DialogResult = true;
        Close();
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

    private record ShellDisplayItem(string Name, string? Path);
}
