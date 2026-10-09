using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TileShop.UI.Views;

/// <summary>
/// Shown in place of the shell when bootstrapping fails; closing it exits the app
/// </summary>
public partial class StartupErrorWindow : Window
{
    public StartupErrorWindow() : this(string.Empty, string.Empty)
    {
    }

    public StartupErrorWindow(string message, string logDirectory)
    {
        InitializeComponent();
        MessageText.Text = message;
        LogDirectoryText.Text = logDirectory;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
