using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Cmux.Core.Services;

namespace Cmux.Controls;

public class PaletteItem
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Shortcut { get; set; }
    public string Icon { get; set; } = "\uE756";
    public string Category { get; set; } = "Commands";
    public Action? Execute { get; set; }
}

/// <summary>
/// Wraps a <see cref="PaletteItem"/> with fuzzy match score and the indices of
/// matched characters in the label, used for sorting and highlighting.
/// </summary>
public record ScoredPaletteItem(PaletteItem Item, int Score, List<int> MatchedIndices);

/// <summary>
/// Attached property that populates a <see cref="TextBlock"/>'s Inlines with highlighted
/// runs based on fuzzy match indices. Bind <see cref="HighlightSource"/> to a
/// <see cref="ScoredPaletteItem"/> and the TextBlock will show the label with
/// matched characters highlighted using the AccentBrush.
/// </summary>
public static class HighlightBehavior
{
    public static readonly DependencyProperty HighlightSourceProperty =
        DependencyProperty.RegisterAttached(
            "HighlightSource",
            typeof(ScoredPaletteItem),
            typeof(HighlightBehavior),
            new PropertyMetadata(null, OnHighlightSourceChanged));

    public static ScoredPaletteItem? GetHighlightSource(DependencyObject obj) =>
        (ScoredPaletteItem?)obj.GetValue(HighlightSourceProperty);

    public static void SetHighlightSource(DependencyObject obj, ScoredPaletteItem? value) =>
        obj.SetValue(HighlightSourceProperty, value);

    private static void OnHighlightSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;

        tb.Inlines.Clear();

        if (e.NewValue is not ScoredPaletteItem scored)
        {
            tb.Text = string.Empty;
            return;
        }

        var label = scored.Item.Label;
        var matchedSet = new HashSet<int>(scored.MatchedIndices);

        if (matchedSet.Count == 0)
        {
            tb.Inlines.Add(new Run(label));
            return;
        }

        Brush? accentBrush = null;
        try { accentBrush = (Brush)Application.Current.FindResource("AccentBrush"); }
        catch { /* fallback below */ }
        accentBrush ??= Brushes.DodgerBlue;

        // Build runs: group consecutive characters with the same highlight state
        int i = 0;
        while (i < label.Length)
        {
            bool isHighlighted = matchedSet.Contains(i);
            int start = i;
            while (i < label.Length && matchedSet.Contains(i) == isHighlighted)
                i++;

            var run = new Run(label[start..i]);
            if (isHighlighted)
            {
                run.Foreground = accentBrush;
                run.FontWeight = FontWeights.Bold;
            }
            tb.Inlines.Add(run);
        }
    }
}

public partial class CommandPalette : UserControl
{
    private List<PaletteItem> _allItems = [];
    public ObservableCollection<ScoredPaletteItem> FilteredItems { get; } = [];

    public event Action? PaletteClosed;
    public event Action<PaletteItem>? ItemExecuted;

    public CommandPalette()
    {
        InitializeComponent();
        ResultsList.ItemsSource = FilteredItems;
    }

    public void Show(List<PaletteItem> items)
    {
        _allItems = items;
        SearchInput.Text = string.Empty;
        Filter(string.Empty);
        Visibility = Visibility.Visible;
        SearchInput.Focus();
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        PaletteClosed?.Invoke();
    }

    public void Filter(string query)
    {
        FilteredItems.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            // No query: show all items with no highlighting
            foreach (var item in _allItems.Take(20))
                FilteredItems.Add(new ScoredPaletteItem(item, 0, []));
        }
        else
        {
            // Score each item using fuzzy matching across label, description, and category
            var scored = _allItems
                .Select(item =>
                {
                    var labelResult = FuzzyMatcher.Score(query, item.Label);
                    var multiResult = FuzzyMatcher.ScoreMultiField(query, item.Label, item.Description, item.Category);
                    // Use multi-field score for ranking, but label-specific indices for highlighting
                    return new ScoredPaletteItem(item, multiResult.Score, labelResult.MatchedIndices);
                })
                .Where(s => s.Score > 0)
                .OrderByDescending(s => s.Score)
                .Take(20);

            foreach (var item in scored)
                FilteredItems.Add(item);
        }

        EmptyText.Visibility = FilteredItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = FilteredItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (FilteredItems.Count > 0)
            ResultsList.SelectedIndex = 0;
    }

    private void ExecuteSelected()
    {
        if (ResultsList.SelectedItem is ScoredPaletteItem scored)
            ExecuteItem(scored.Item);
    }

    private void ExecuteItem(PaletteItem item)
    {
        Hide();
        item.Execute?.Invoke();
        ItemExecuted?.Invoke(item);
    }

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        => Filter(SearchInput.Text);

    private void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.Enter:
                ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Down:
                if (ResultsList.SelectedIndex < FilteredItems.Count - 1)
                    ResultsList.SelectedIndex++;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up:
                if (ResultsList.SelectedIndex > 0)
                    ResultsList.SelectedIndex--;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => ExecuteSelected();

    private void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var container = ItemsControl.ContainerFromElement(ResultsList, source) as ListBoxItem;
        if (container?.DataContext is ScoredPaletteItem scored)
        {
            ExecuteItem(scored.Item);
            e.Handled = true;
        }
    }
}
