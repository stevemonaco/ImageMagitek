using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using TileShop.Shared.Dialogs;
using TileShop.AvaloniaUI.Windowing;
using MessageBox.Avalonia.ViewModels;
using MessageBox.Avalonia.Views;

namespace TileShop.AvaloniaUI.Windowing;
internal class WindowManager : IWindowManager
{
    private ViewLocator _viewLocator;

    public WindowManager(ViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    /// <inheritdoc/>
    public async Task<PromptResult> ShowMessageBox(string contentMessage, PromptChoice userChoices, string title = "")
    {
        var boxChoices = ChoiceToButton(userChoices);

        var box = MessageBoxManager.GetMessageBoxStandardWindow(
            new()
            {
                ButtonDefinitions = boxChoices,
                ContentTitle = title,
                ContentMessage = contentMessage + $"{Environment.NewLine}",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = true,
                ShowInCenter = true,
                Topmost = true,
            });

        var mainWindow = GetWindow();

        var boxResult = await box.ShowDialog(mainWindow);
        var userResult = ButtonResultToChoice(boxResult);

        return userResult;
    }

    /// <inheritdoc/>
    public async Task ShowMessageBox(string contentMessage, string title = "")
    {
        var box = MessageBoxManager.GetMessageBoxStandardWindow(
            new()
            {
                Width = 800,
                ButtonDefinitions = ButtonEnum.Ok,
                ContentTitle = title,
                ContentMessage = contentMessage + $"{Environment.NewLine}", // New line because SizeToContent is broken in Avalonia/MessageBox.Avalonia
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = true,
                ShowInCenter = true,
                Topmost = true,
            });

        var mainWindow = GetWindow();

        await box.ShowDialog(mainWindow);
    }

    /// <inheritdoc/>
    public PromptResult ShowMessageBoxSync(string contentMessage, PromptChoice userChoices, string title = "")
    {
        var boxChoices = ChoiceToButton(userChoices);

        var boxParameters = new MessageBox.Avalonia.DTO.MessageBoxStandardParams()
        {
            ButtonDefinitions = boxChoices,
            ContentTitle = title,
            ContentMessage = contentMessage + $"{Environment.NewLine}",
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            ShowInCenter = true,
            Topmost = true,
        };

        var boxWindow = new MsBoxStandardWindow();
        var boxViewModel = new MsBoxStandardViewModel(boxParameters, boxWindow);
        boxWindow.DataContext = boxViewModel;

        var mainWindow = GetWindow();

        boxWindow.ShowDialogSync(mainWindow);
        var boxResult = boxWindow.ButtonResult;

        var userResult = ButtonResultToChoice(boxResult);

        return userResult;
    }

    /// <inheritdoc/>
    public void ShowWindow(object viewModel)
    {
        var mainWindow = GetWindow();

        var newWindow = (Window)_viewLocator.Build(viewModel);
        newWindow.DataContext = viewModel;
        newWindow.Show(mainWindow);
    }

    /// <inheritdoc/>
    public async Task<TResult> ShowDialog<TResult>(IDialogMediator<TResult> mediator)
    {
        var mainWindow = GetWindow();

        var dialogWindow = new DialogView
        {
            DataContext = mediator,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

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

    private ButtonEnum ChoiceToButton(PromptChoice choice)
    {
        return choice switch
        {
            PromptChoice.Ok => ButtonEnum.Ok,
            PromptChoice.OkCancel => ButtonEnum.OkCancel,
            PromptChoice.YesNo => ButtonEnum.YesNo,
            PromptChoice.YesNoCancel => ButtonEnum.YesNoCancel
        };
    }

    private PromptResult ButtonResultToChoice(ButtonResult result)
    {
        return result switch
        {
            ButtonResult.Ok => PromptResult.Ok,
            ButtonResult.Cancel => PromptResult.Cancel,
            ButtonResult.Yes => PromptResult.Yes,
            ButtonResult.No => PromptResult.No
        };
    }
}
