using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cmux.Core.Models;
using Cmux.ViewModels;

namespace Cmux.Controls;

public partial class WorkspaceGroupHeader : UserControl
{
    public WorkspaceGroupHeader()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateArrow();
    }

    private MainViewModel? FindMainViewModel()
    {
        var window = Window.GetWindow(this);
        return window?.DataContext as MainViewModel;
    }

    private void OnHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WorkspaceGroup group)
        {
            FindMainViewModel()?.ToggleGroupCollapsed(group.Id);
            UpdateArrow();
        }
    }

    private void UpdateArrow()
    {
        if (DataContext is WorkspaceGroup group)
            ArrowRotation.Angle = group.IsCollapsed ? -90 : 0;
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkspaceGroup group) return;
        RenameBox.Text = group.Name;
        NameText.Visibility = Visibility.Collapsed;
        RenameBox.Visibility = Visibility.Visible;
        RenameBox.Focus();
        RenameBox.SelectAll();
    }

    private void FinishRename()
    {
        if (DataContext is WorkspaceGroup group && !string.IsNullOrWhiteSpace(RenameBox.Text))
            group.Name = RenameBox.Text.Trim();
        NameText.Visibility = Visibility.Visible;
        RenameBox.Visibility = Visibility.Collapsed;
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { FinishRename(); e.Handled = true; }
        else if (e.Key == Key.Escape) { NameText.Visibility = Visibility.Visible; RenameBox.Visibility = Visibility.Collapsed; e.Handled = true; }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e) => FinishRename();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceGroup group)
            FindMainViewModel()?.DeleteGroup(group.Id);
    }
}
