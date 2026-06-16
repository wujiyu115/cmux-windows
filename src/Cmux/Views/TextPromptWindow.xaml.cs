using System.Windows;
using System.Windows.Input;

namespace Cmux.Views;

public partial class TextPromptWindow : Window
{
    private readonly bool _multiLine;

    public string ResponseText => InputTextBox.Text;

    public TextPromptWindow(string title, string message, string? defaultValue = null, bool multiLine = false)
    {
        InitializeComponent();
        WindowAppearance.Apply(this);

        _multiLine = multiLine;
        Title = title;
        PromptText.Text = message;
        InputTextBox.Text = defaultValue ?? string.Empty;

        if (multiLine)
        {
            InputTextBox.AcceptsReturn = true;
            InputTextBox.TextWrapping = TextWrapping.NoWrap;
            InputTextBox.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            InputTextBox.MinHeight = 100;
            InputTextBox.MaxHeight = 200;
            Height = 340;
        }

        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_multiLine)
        {
            Ok_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
    }
}
