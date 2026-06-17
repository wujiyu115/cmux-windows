using System.Windows;
using System.Windows.Media;

namespace Cmux.Views;

public partial class ThemedMessageBox : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    private ThemedMessageBox()
    {
        InitializeComponent();
    }

    public static MessageBoxResult Show(string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        Window? owner = null)
    {
        var dlg = new ThemedMessageBox();
        dlg.TitleText.Text = title;
        dlg.MessageText.Text = message;
        dlg.Title = title;

        if (owner != null)
            dlg.Owner = owner;
        else if (Application.Current?.MainWindow is { IsLoaded: true } mw)
            dlg.Owner = mw;

        // Icon
        switch (icon)
        {
            case MessageBoxImage.Information:
                dlg.IconText.Text = "";
                dlg.IconText.Foreground = (Application.Current?.FindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue);
                break;
            case MessageBoxImage.Warning:
                dlg.IconText.Text = "";
                dlg.IconText.Foreground = (Application.Current?.FindResource("WarningBrush") as Brush ?? Brushes.Orange);
                break;
            case MessageBoxImage.Error:
                dlg.IconText.Text = "";
                dlg.IconText.Foreground = (Application.Current?.FindResource("ErrorBrush") as Brush ?? Brushes.Red);
                break;
            case MessageBoxImage.Question:
                dlg.IconText.Text = "";
                dlg.IconText.Foreground = (Application.Current?.FindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue);
                break;
            default:
                dlg.IconText.Visibility = Visibility.Collapsed;
                break;
        }

        // Buttons
        switch (buttons)
        {
            case MessageBoxButton.OK:
                dlg.BtnOk.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.OKCancel:
                dlg.BtnOk.Visibility = Visibility.Visible;
                dlg.BtnCancel.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.YesNo:
                dlg.BtnOk.Visibility = Visibility.Collapsed;
                dlg.BtnYes.Visibility = Visibility.Visible;
                dlg.BtnNo.Visibility = Visibility.Visible;
                break;
            case MessageBoxButton.YesNoCancel:
                dlg.BtnOk.Visibility = Visibility.Collapsed;
                dlg.BtnYes.Visibility = Visibility.Visible;
                dlg.BtnNo.Visibility = Visibility.Visible;
                dlg.BtnCancel.Visibility = Visibility.Visible;
                break;
        }

        dlg.ShowDialog();
        return dlg.Result;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        Close();
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Yes;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.None;
        Close();
    }
}
