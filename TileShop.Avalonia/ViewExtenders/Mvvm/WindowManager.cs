using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TileShop.AvaloniaUI.ViewExtenders;

/// <summary>
/// Manager capable of taking a ViewModel instance, instantiating its View and showing it as a dialog or window
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// Given a ViewModel, show its corresponding View as a window
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    void ShowWindow(object viewModel);

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a Dialog
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    /// <returns>DialogResult of the View</returns>
    Task<TResult> ShowDialog<TResult>(IDialogMediator<TResult> mediator);
    
    /// <summary>
    /// Display a MessageBox
    /// </summary>
    /// <param name="messageBoxText">A <see cref="System.String"/> that specifies the text to display.</param>
    /// <param name="caption">A <see cref="System.String"/> that specifies the title bar caption to display.</param>
    /// <param name="buttonLabels">A dictionary specifying the button labels, if desirable</param>
    /// <returns>The result chosen by the user</returns>
    MessageBoxResult ShowMessageBox(string messageBoxText, string caption = "", IDictionary<MessageBoxResult, string> buttonLabels = null);
}

internal class WindowManager : IWindowManager
{
    private ViewLocator _viewLocator;

    public WindowManager(ViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    public MessageBoxResult ShowMessageBox(string messageBoxText, string caption = "", IDictionary<MessageBoxResult, string> buttonLabels = null)
    {
        throw new NotImplementedException();
    }

    public void ShowWindow(object viewModel)
    {
        var mainWindow = GetWindow();

        var newWindow = (Window)_viewLocator.Build(viewModel);
        newWindow.DataContext = viewModel;
        newWindow.Show(mainWindow);
    }
    
    public async Task<TResult> ShowDialog<TResult>(IDialogMediator<TResult> mediator)
    {
        var mainWindow = GetWindow();

        var dialogWindow = new DialogView();
        dialogWindow.DataContext = mediator;

        mediator.PropertyChanged += Handler;
        await dialogWindow.ShowDialog(mainWindow);
        mediator.PropertyChanged -= Handler;

        return mediator.DialogResult!;

        void Handler(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "DialogResult")
                return;

            dialogWindow.Close();
        }
    }

    private static Window? GetWindow()
    {
        var lifetime = Avalonia.Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return lifetime?.MainWindow;
    }
}
